using System.Data;
using System.Security.Cryptography;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Application.Integration;
using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CreatorPay.Infrastructure.Integration;

public sealed record ExternalSessionContext(ExternalSessionSnapshot Session, ExternalIdentity Identity,
    ExternalProfileLink? Profile, UserAccount? User, string? AccountEmail = null,
    string? AccountPhone = null);

public sealed class ExternalIntegrationService(ApplicationDbContext db, IV3AuthorityClient authority,
    V3IntegrationOptions options, IUtcClock clock)
{
    public bool Enabled => options.Enabled;
    public string V3ContinuationUrl => options.V3WebUrl.TrimEnd('/') + "/product-handoff";
    public string V3ProfileSelectionUrl => options.V3WebUrl.TrimEnd('/') + "/onboarding";
    public string V3SignOutUrl => options.V3WebUrl.TrimEnd('/') + "/integration/sign-out";
    public string ProductWebUrl => options.ProductWebUrl.TrimEnd('/');
    public string CallbackId => options.CallbackId;

    public async Task<ExternalSessionContext> RedeemAsync(string code, string callbackId, CancellationToken ct)
    {
        EnsureEnabled();
        if (callbackId != options.CallbackId || code.Length is < 40 or > 100)
            throw new UnauthorizedAccessException("The Weymela handoff is invalid or expired.");
        var assertion = await authority.RedeemAsync(code, callbackId, ct);
        Validate(assertion);
        return await PersistAssertionAsync(assertion, ct);
    }

    private async Task<ExternalSessionContext> PersistAssertionAsync(V3IdentityAssertion assertion,
        CancellationToken ct, bool retry = true)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var now = clock.UtcNow;
            var identity = await db.ExternalIdentities.SingleOrDefaultAsync(x => x.Issuer == assertion.Issuer
                && x.Environment == assertion.Environment && x.ExternalUserId == assertion.UserId, ct);
            if (identity is null)
            {
                identity = new ExternalIdentity
                {
                    Id = Guid.NewGuid(),
                    Issuer = assertion.Issuer,
                    Environment = assertion.Environment,
                    ExternalUserId = assertion.UserId,
                    IdentityBindingId = assertion.IdentityBindingId,
                    IdentityBindingVersion = assertion.IdentityBindingVersion,
                    LastValidatedAtUtc = now,
                    CreatedAtUtc = now,
                    CreatedBy = "V3External"
                };
                db.ExternalIdentities.Add(identity);
            }
            else
            {
                if (!identity.IsActive || assertion.IdentityBindingVersion < identity.IdentityBindingVersion)
                    throw new UnauthorizedAccessException("The Weymela identity is not active.");
                identity.IdentityBindingId = assertion.IdentityBindingId;
                identity.IdentityBindingVersion = assertion.IdentityBindingVersion;
                identity.LastValidatedAtUtc = now;
                identity.UpdatedAtUtc = now;
                identity.UpdatedBy = "V3External";
            }
            var role = MapRole(assertion.Role);
            ExternalProfileLink? link;
            if (assertion.Purpose == V3HandoffPurposes.ProfileOnboarding)
            {
                if (role is UserRole.PlatformAdmin or UserRole.Cashier)
                    throw new UnauthorizedAccessException("This profile cannot be onboarded here.");
                link = role == UserRole.MerchantAdmin
                    ? await db.ExternalProfileLinks.Where(x => x.ExternalIdentityId == identity.Id && x.Role == role
                            && (x.Status == ExternalProfileStatus.Onboarding || x.Status == ExternalProfileStatus.Pending))
                        .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct)
                    : await db.ExternalProfileLinks.SingleOrDefaultAsync(x => x.ExternalIdentityId == identity.Id
                        && x.Role == role, ct);
                link ??= new ExternalProfileLink
                {
                    Id = Guid.NewGuid(),
                    ExternalIdentityId = identity.Id,
                    Role = role,
                    Status = ExternalProfileStatus.Onboarding,
                    ProvisioningKey = role == UserRole.MerchantAdmin
                        ? $"v3:{identity.Id:N}:{role}:onboarding:{Guid.NewGuid():N}"
                        : $"v3:{identity.Id:N}:{role}:onboarding",
                    CreatedAtUtc = now,
                    CreatedBy = "V3External"
                };
                if (db.Entry(link).State == EntityState.Detached) db.ExternalProfileLinks.Add(link);
            }
            else
            {
                if (assertion.Purpose != V3HandoffPurposes.ExistingWorkspace || assertion.ProfileSubjectId is null)
                    throw new UnauthorizedAccessException("The Weymela workspace authority is invalid.");
                link = await db.ExternalProfileLinks.SingleOrDefaultAsync(x => x.ExternalIdentityId == identity.Id
                    && x.Role == role && x.ExternalProfileSubjectId == assertion.ProfileSubjectId, ct);
                if (role == UserRole.PlatformAdmin)
                    link = await EnsurePlatformAdminLinkAsync(identity, assertion, link, now, ct);
                else if (role == UserRole.Customer && link is null)
                    link = await CreateMappedCustomerAsync(identity, assertion, now, ct);
                if (link?.Status != ExternalProfileStatus.Active || link.UserAccountId is null)
                    throw new UnauthorizedAccessException("This Weymela workspace is not mapped or active.");
            }
            await db.SaveChangesAsync(ct);
            var session = new ExternalApplicationSession
            {
                Id = Guid.NewGuid(),
                ExternalIdentityId = identity.Id,
                ExternalProfileLinkId = link.Id,
                UserAccountId = link.UserAccountId,
                IdentityBindingId = assertion.IdentityBindingId,
                IdentityBindingVersion = assertion.IdentityBindingVersion,
                Role = role,
                Purpose = assertion.Purpose,
                LastValidatedAtUtc = now,
                ExpiresAtUtc = now.Add(options.SessionLifetime),
                CreatedAtUtc = now,
                CreatedBy = "V3External"
            };
            db.ExternalApplicationSessions.Add(session);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            var user = session.UserAccountId is Guid userId
                ? await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == userId, ct) : null;
            return Context(session, identity, link, user, assertion.AccountEmail, assertion.AccountPhone);
        }
        catch (Exception exception) when (retry && IsRetryable(exception))
        {
            db.ChangeTracker.Clear();
            return await PersistAssertionAsync(assertion, ct, false);
        }
    }

    public async Task<ExternalSessionContext?> ValidateSessionAsync(Guid sessionId, CancellationToken ct)
    {
        if (!options.Enabled || sessionId == Guid.Empty) return null;
        var session = await db.ExternalApplicationSessions.SingleOrDefaultAsync(x => x.Id == sessionId, ct);
        var now = clock.UtcNow;
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= now) return null;
        var identity = await db.ExternalIdentities.SingleOrDefaultAsync(x => x.Id == session.ExternalIdentityId, ct);
        var link = session.ExternalProfileLinkId is Guid linkId
            ? await db.ExternalProfileLinks.SingleOrDefaultAsync(x => x.Id == linkId, ct) : null;
        if (identity is null || !identity.IsActive || link is null) return null;
        if (now - session.LastValidatedAtUtc >= options.RevalidationInterval)
        {
            var current = await authority.RevalidateAsync(new(identity.ExternalUserId, session.IdentityBindingId,
                session.IdentityBindingVersion, V3Role(session.Role), link.ExternalProfileSubjectId,
                link.ExternalBusinessId, session.Purpose), ct);
            if (!current.Active)
            {
                session.RevokedAtUtc = now; session.RevokedReason = "V3 authority revoked";
                await db.SaveChangesAsync(ct); return null;
            }
            session.LastValidatedAtUtc = now; identity.LastValidatedAtUtc = now;
            await db.SaveChangesAsync(ct);
        }
        var user = session.UserAccountId is Guid userId
            ? await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct) : null;
        if (user is not null && (user.AuthenticationSource != AuthenticationSource.V3External
            || user.Status is AccountStatus.Suspended or AccountStatus.Rejected or AccountStatus.Closed)) return null;
        return Context(session, identity, link, user);
    }

    public async Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken ct)
    {
        var session = await db.ExternalApplicationSessions.SingleOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null || session.RevokedAtUtc is not null) return;
        session.RevokedAtUtc = clock.UtcNow; session.RevokedReason = reason;
        await db.SaveChangesAsync(ct);
    }

    public Task<ExternalProvisioningResult> ProvisionCustomerAsync(Guid sessionId,
        ExternalCustomerRegistration request, CancellationToken ct) => ProvisionAsync(sessionId,
        UserRole.Customer, request.DisplayName, async (identity, link, now, innerCt) =>
        {
            if (request.DisplayName.Trim().Length is < 1 or > 200) throw new ArgumentException("Preferred name is required.");
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                PublicCustomerId = $"CUS-{Guid.NewGuid():N}",
                DisplayName = request.DisplayName.Trim(),
                PhoneNumber = "",
                NormalizedPhoneNumber = "",
                Status = CustomerStatus.Active,
                CreatedAtUtc = now,
                CreatedBy = "V3External"
            };
            var user = ExternalUser(UserRole.Customer, AccountStatus.Active, now);
            user.CustomerId = customer.Id; user.DisplayName = customer.DisplayName;
            db.Add(customer); db.Add(user);
            db.Add(new CustomerWallet
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                CurrencyCode = "ETB",
                CreatedAtUtc = now,
                CreatedBy = "V3External"
            });
            return (user, customer.Id, customer.DisplayName, "ACTIVE", "/shopper");
        }, ct);

    public Task<ExternalProvisioningResult> ProvisionCreatorAsync(Guid sessionId,
        ExternalCreatorRegistration request, CancellationToken ct) => ProvisionAsync(sessionId,
        UserRole.Creator, request.DisplayName, async (identity, link, now, innerCt) =>
        {
            var socialProfiles = NormalizeSocialProfiles(request.SocialProfiles);
            if (request.FirstName.Trim().Length is < 1 or > 100 || request.LastName.Trim().Length is < 1 or > 100
                || request.DisplayName.Trim().Length is < 2 or > 200 || request.City.Trim().Length is < 1 or > 120
                || request.Biography.Trim().Length > 1000 || request.ContentCategories.Trim().Length > 500)
                throw new ArgumentException("Complete the required Creator details.");
            var creatorId = Guid.NewGuid();
            var creator = new Creator
            {
                Id = creatorId,
                PublicCreatorId = $"CR-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
                CreatorCode = await AllocateCreatorCodeAsync(innerCt),
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                DisplayName = request.DisplayName.Trim(),
                PhoneNumber = "",
                NormalizedPhoneNumber = "",
                Email = "",
                PreferredLanguage = "en",
                City = request.City.Trim(),
                Zone = request.Zone?.Trim(),
                Biography = request.Biography.Trim(),
                ContentCategories = request.ContentCategories.Trim(),
                TermsAcceptedAtUtc = now,
                Status = CreatorStatus.PendingApproval,
                CreatedAtUtc = now,
                CreatedBy = "V3External"
            };
            for (var index = 0; index < socialProfiles.Count; index++)
            {
                var social = socialProfiles[index];
                creator.SocialProfiles.Add(new CreatorSocialProfile
                {
                    Id = Guid.NewGuid(), CreatorId = creatorId, Platform = social.Platform,
                    Handle = social.ProfileUrl.ToUpperInvariant(), ProfileUrl = social.ProfileUrl,
                    FollowerCount = social.AudienceCount, IsPrimary = index == 0,
                    VerificationStatus = SocialProfileVerificationStatus.Unverified,
                    CreatedAtUtc = now, CreatedBy = "V3External"
                });
            }
            var user = ExternalUser(UserRole.Creator, AccountStatus.PendingApproval, now);
            user.CreatorId = creatorId; user.DisplayName = creator.DisplayName;
            db.Add(creator); db.Add(user);
            return (user, creatorId, creator.DisplayName, "PENDING", "/creator");
        }, ct);

    public Task<ExternalProvisioningResult> ProvisionBusinessAsync(Guid sessionId,
        ExternalBusinessRegistration request, CancellationToken ct) => ProvisionAsync(sessionId,
        UserRole.MerchantAdmin, request.TradingName, async (identity, link, now, innerCt) =>
        {
            var phone = string.IsNullOrWhiteSpace(request.BusinessContactPhone) ? ""
                : EthiopianMobileNumber.Normalize(request.BusinessContactPhone);
            var contactEmail = request.BusinessContactEmail?.Trim();
            if (contactEmail?.Length == 0) contactEmail = null;
            if (contactEmail is not null && (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                    .IsValid(contactEmail) || contactEmail.Length > 320))
                throw new ArgumentException("Business contact email is invalid.");
            if (request.TradingName.Trim().Length is < 2 or > 250 || !BusinessTypes.IsSupported(request.BusinessType.Trim())
                || request.PrimaryContactName.Trim().Length is < 1 or > 200 || request.BusinessAddress.Trim().Length < 1
                || request.City.Trim().Length < 1 || request.Region.Trim().Length < 1
                || request.Country.Trim().Length < 1) throw new ArgumentException("Complete the required Business details.");
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone.Trim()); }
            catch { throw new ArgumentException("Time zone is invalid."); }
            var merchantId = Guid.NewGuid();
            var merchant = new Merchant
            {
                Id = merchantId,
                PublicMerchantId = $"ME-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
                LegalBusinessName = request.TradingName.Trim(),
                TradingName = request.TradingName.Trim(),
                BusinessType = request.BusinessType.Trim(),
                PrimaryContactName = request.PrimaryContactName.Trim(),
                PhoneNumber = phone,
                NormalizedPhoneNumber = phone,
                Email = contactEmail,
                PreferredLanguage = "en",
                TermsAcceptedAtUtc = now,
                BusinessAddress = request.BusinessAddress.Trim(),
                City = request.City.Trim(),
                Region = request.Region.Trim(),
                Country = request.Country.Trim(),
                TimeZone = request.TimeZone.Trim(),
                Status = MerchantStatus.PendingReview,
                CreatedAtUtc = now,
                CreatedBy = "V3External"
            };
            var user = ExternalUser(UserRole.MerchantAdmin, AccountStatus.PendingApproval, now);
            user.MerchantId = merchantId; user.DisplayName = merchant.TradingName;
            db.Add(merchant); db.Add(user);
            return (user, merchantId, merchant.TradingName, "PENDING", "/business");
        }, ct);

    public async Task<IReadOnlyList<ExternalCreatorSocialProfileResult>> UpdateCreatorSocialProfilesAsync(
        Guid sessionId, ExternalCreatorSocialProfilesUpdate request, CancellationToken ct)
    {
        var context = await ValidateSessionAsync(sessionId, ct)
            ?? throw new UnauthorizedAccessException("The Weymela product session has expired.");
        if (context.Session.Role != UserRole.Creator || context.User?.CreatorId is not Guid creatorId
            || context.User.Status != AccountStatus.Active
            || context.Profile?.Status != ExternalProfileStatus.Active)
            throw new UnauthorizedAccessException("An active Creator profile is required.");
        var normalized = NormalizeSocialProfiles(request.SocialProfiles);
        var creator = await db.Creators.Include(x => x.SocialProfiles)
            .SingleOrDefaultAsync(x => x.Id == creatorId, ct)
            ?? throw new InvalidOperationException("The Creator profile is unavailable.");
        var now = clock.UtcNow;
        db.CreatorSocialProfiles.RemoveRange(creator.SocialProfiles);
        for (var index = 0; index < normalized.Count; index++)
        {
            var social = normalized[index];
            db.CreatorSocialProfiles.Add(new CreatorSocialProfile
            {
                Id = Guid.NewGuid(), CreatorId = creator.Id, Platform = social.Platform,
                Handle = social.ProfileUrl.ToUpperInvariant(), ProfileUrl = social.ProfileUrl,
                FollowerCount = social.AudienceCount, IsPrimary = index == 0,
                VerificationStatus = SocialProfileVerificationStatus.Unverified,
                CreatedAtUtc = now, CreatedBy = context.User.Id.ToString()
            });
        }
        creator.UpdatedAtUtc = now; creator.UpdatedBy = context.User.Id.ToString();
        await db.SaveChangesAsync(ct);
        return normalized.Select((social, index) => new ExternalCreatorSocialProfileResult(
            social.Platform, social.ProfileUrl, social.AudienceCount,
            SocialProfileVerificationStatus.Unverified, index == 0)).ToArray();
    }

    public async Task<bool> SynchronizeLifecycleAsync(UserRole role, Guid profileId, string lifecycle, CancellationToken ct)
    {
        var user = role switch
        {
            UserRole.Creator => await db.UserAccounts.SingleOrDefaultAsync(x => x.CreatorId == profileId, ct),
            UserRole.MerchantAdmin => await db.UserAccounts.SingleOrDefaultAsync(x => x.MerchantId == profileId, ct),
            _ => null
        };
        if (user?.AuthenticationSource != AuthenticationSource.V3External) return false;
        var actualLifecycle = user.Status switch
        {
            AccountStatus.Active => "ACTIVE",
            AccountStatus.Rejected => "REJECTED",
            _ => "PENDING"
        };
        if (!string.Equals(actualLifecycle, lifecycle, StringComparison.OrdinalIgnoreCase)) return false;
        var link = await db.ExternalProfileLinks.SingleAsync(x => x.UserAccountId == user.Id, ct);
        var identity = await db.ExternalIdentities.SingleAsync(x => x.Id == link.ExternalIdentityId, ct);
        var result = await authority.SynchronizeAsync(new(identity.ExternalUserId, identity.IdentityBindingId,
            identity.IdentityBindingVersion, V3Role(role), profileId,
            role == UserRole.MerchantAdmin ? profileId : null, user.DisplayName ?? V3Role(role), lifecycle,
            link.ProvisioningKey), ct);
        link.ExternalProfileSubjectId = result.V3ProfileSubjectId;
        link.ExternalBusinessId = role == UserRole.MerchantAdmin ? result.V3ProfileSubjectId : null;
        link.Status = lifecycle.ToUpperInvariant() switch
        {
            "ACTIVE" => ExternalProfileStatus.Active,
            "REJECTED" => ExternalProfileStatus.Rejected,
            _ => ExternalProfileStatus.Pending
        };
        link.UpdatedAtUtc = clock.UtcNow; link.UpdatedBy = "V3External";
        if (link.Status == ExternalProfileStatus.Active)
        {
            await db.ExternalApplicationSessions.Where(x => x.ExternalProfileLinkId == link.Id
                    && x.RevokedAtUtc == null && x.ExpiresAtUtc > clock.UtcNow)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.Purpose, V3HandoffPurposes.ExistingWorkspace), ct);
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<ExternalProvisioningResult> ProvisionAsync(Guid sessionId, UserRole expectedRole,
        string displayName, Func<ExternalIdentity, ExternalProfileLink, DateTime, CancellationToken,
            Task<(UserAccount User, Guid ProfileId, string DisplayName, string Lifecycle, string Destination)>> create,
        CancellationToken ct, bool retry = true)
    {
        var context = await ValidateSessionAsync(sessionId, ct)
            ?? throw new UnauthorizedAccessException("The Weymela product session has expired.");
        if (context.Session.Role != expectedRole || context.Profile is null
            || context.Session.Purpose != V3HandoffPurposes.ProfileOnboarding
                && (context.Session.Purpose != V3HandoffPurposes.ExistingWorkspace
                    || context.Profile.UserAccountId is null))
            throw new UnauthorizedAccessException("This onboarding session is not authorized.");
        if (context.Profile.UserAccountId is Guid existingUserId)
        {
            var profileId = await ProfileIdAsync(existingUserId, expectedRole, ct);
            var existingUser = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == existingUserId, ct);
            return await SynchronizeProvisionedAsync(context.Identity, context.Profile, existingUser,
                profileId, Lifecycle(existingUser, context.Profile), Destination(expectedRole), sessionId, ct);
        }
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var loaded = await db.ExternalProfileLinks.SingleAsync(x => x.Id == context.Profile.Id, ct);
            if (loaded.UserAccountId is Guid racedUserId)
            {
                await transaction.CommitAsync(ct);
                var racedUser = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Id == racedUserId, ct);
                return await SynchronizeProvisionedAsync(context.Identity, loaded, racedUser,
                    await ProfileIdAsync(racedUserId, expectedRole, ct), Lifecycle(racedUser, loaded),
                    Destination(expectedRole), sessionId, ct);
            }
            var now = clock.UtcNow;
            var built = await create(context.Identity, loaded, now, ct);
            loaded.UserAccountId = built.User.Id;
            loaded.Status = built.Lifecycle == "ACTIVE" ? ExternalProfileStatus.Active : ExternalProfileStatus.Pending;
            loaded.UpdatedAtUtc = now; loaded.UpdatedBy = "V3External";
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await SynchronizeProvisionedAsync(context.Identity, loaded, built.User, built.ProfileId,
                built.Lifecycle, built.Destination, sessionId, ct);
        }
        catch (Exception exception) when (retry && IsRetryable(exception))
        {
            db.ChangeTracker.Clear();
            return await ProvisionAsync(sessionId, expectedRole, displayName, create, ct, false);
        }
    }

    private async Task<ExternalProvisioningResult> SynchronizeProvisionedAsync(ExternalIdentity identity,
        ExternalProfileLink link, UserAccount user, Guid profileId, string lifecycle, string destination,
        Guid sessionId, CancellationToken ct)
    {
        if (link.ExternalProfileSubjectId is null || link.Status != Status(lifecycle))
        {
            var synchronized = await authority.SynchronizeAsync(new(identity.ExternalUserId,
                identity.IdentityBindingId, identity.IdentityBindingVersion, V3Role(link.Role), profileId,
                link.Role == UserRole.MerchantAdmin ? profileId : null, user.DisplayName ?? V3Role(link.Role),
                lifecycle, link.ProvisioningKey), ct);
            link.ExternalProfileSubjectId = synchronized.V3ProfileSubjectId;
            link.ExternalBusinessId = link.Role == UserRole.MerchantAdmin ? synchronized.V3ProfileSubjectId : null;
            link.Status = Status(synchronized.Lifecycle);
            link.UpdatedAtUtc = clock.UtcNow;
            link.UpdatedBy = "V3External";
        }
        var session = await db.ExternalApplicationSessions.SingleAsync(x => x.Id == sessionId, ct);
        session.UserAccountId = user.Id;
        if (link.Status == ExternalProfileStatus.Active)
            session.Purpose = V3HandoffPurposes.ExistingWorkspace;
        await db.SaveChangesAsync(ct);
        return new(user.Id, profileId, lifecycle, destination);
    }

    private static string Lifecycle(UserAccount user, ExternalProfileLink link) =>
        user.Status == AccountStatus.Active || link.Status == ExternalProfileStatus.Active ? "ACTIVE" :
        user.Status == AccountStatus.Rejected || link.Status == ExternalProfileStatus.Rejected ? "REJECTED" : "PENDING";

    private static ExternalProfileStatus Status(string lifecycle) => lifecycle.ToUpperInvariant() switch
    {
        "ACTIVE" => ExternalProfileStatus.Active,
        "REJECTED" => ExternalProfileStatus.Rejected,
        _ => ExternalProfileStatus.Pending
    };

    private async Task<ExternalProfileLink> EnsurePlatformAdminLinkAsync(ExternalIdentity identity,
        V3IdentityAssertion assertion, ExternalProfileLink? existing, DateTime now, CancellationToken ct)
    {
        if (options.PlatformAdminUserAccountId is not Guid configuredId)
            throw new UnauthorizedAccessException("The Platform Admin mapping is not configured.");
        var account = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == configuredId
            && x.Role == UserRole.PlatformAdmin && x.Status == AccountStatus.Active, ct)
            ?? throw new UnauthorizedAccessException("The Platform Admin mapping is unavailable.");
        if (existing is not null && existing.UserAccountId != configuredId)
            throw new UnauthorizedAccessException("The Platform Admin mapping is invalid.");
        if (existing is null)
        {
            existing = new ExternalProfileLink
            {
                Id = Guid.NewGuid(),
                ExternalIdentityId = identity.Id,
                Role = UserRole.PlatformAdmin,
                ExternalProfileSubjectId = assertion.ProfileSubjectId,
                UserAccountId = account.Id,
                Status = ExternalProfileStatus.Active,
                ProvisioningKey = $"v3:{identity.Id:N}:admin",
                CreatedAtUtc = now,
                CreatedBy = "V3External"
            };
            db.ExternalProfileLinks.Add(existing);
        }
        account.AuthenticationSource = AuthenticationSource.V3External;
        account.PasswordHash = string.Empty;
        account.FirebaseUid = null; account.PinHash = null;
        return existing;
    }

    private async Task<ExternalProfileLink> CreateMappedCustomerAsync(ExternalIdentity identity,
        V3IdentityAssertion assertion, DateTime now, CancellationToken ct)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            PublicCustomerId = $"CUS-{Guid.NewGuid():N}",
            DisplayName = assertion.DisplayName,
            PhoneNumber = "",
            NormalizedPhoneNumber = "",
            Status = CustomerStatus.Active,
            CreatedAtUtc = now,
            CreatedBy = "V3External"
        };
        var user = ExternalUser(UserRole.Customer, AccountStatus.Active, now);
        user.CustomerId = customer.Id; user.DisplayName = customer.DisplayName;
        var link = new ExternalProfileLink
        {
            Id = Guid.NewGuid(),
            ExternalIdentityId = identity.Id,
            Role = UserRole.Customer,
            ExternalProfileSubjectId = assertion.ProfileSubjectId,
            UserAccountId = user.Id,
            Status = ExternalProfileStatus.Active,
            ProvisioningKey = $"v3:{identity.Id:N}:customer:{assertion.ProfileSubjectId:N}",
            CreatedAtUtc = now,
            CreatedBy = "V3External"
        };
        db.Add(customer); db.Add(user); db.Add(link);
        db.Add(new CustomerWallet
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            CurrencyCode = "ETB",
            CreatedAtUtc = now,
            CreatedBy = "V3External"
        });
        await Task.CompletedTask;
        return link;
    }

    private async Task<string> AllocateCreatorCodeAsync(CancellationToken ct)
    {
        var start = RandomNumberGenerator.GetInt32(1000, 10000);
        for (var offset = 0; offset < 9000; offset++)
        {
            var code = (1000 + (start - 1000 + offset) % 9000).ToString();
            if (!await db.Creators.AnyAsync(x => x.CreatorCode == code, ct)) return code;
        }
        throw new InvalidOperationException("Creator capacity is unavailable.");
    }

    private static UserAccount ExternalUser(UserRole role, AccountStatus status, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Email = "",
        NormalizedEmail = "",
        PasswordHash = "",
        AuthenticationSource = AuthenticationSource.V3External,
        Role = role,
        Status = status,
        IsEmailVerified = false,
        IsPhoneVerified = false,
        CreatedAtUtc = now,
        CreatedBy = "V3External"
    };
    private async Task<Guid> ProfileIdAsync(Guid userId, UserRole role, CancellationToken ct) => role switch
    {
        UserRole.Customer => await db.UserAccounts.Where(x => x.Id == userId).Select(x => x.CustomerId!.Value).SingleAsync(ct),
        UserRole.Creator => await db.UserAccounts.Where(x => x.Id == userId).Select(x => x.CreatorId!.Value).SingleAsync(ct),
        UserRole.MerchantAdmin => await db.UserAccounts.Where(x => x.Id == userId).Select(x => x.MerchantId!.Value).SingleAsync(ct),
        _ => userId
    };
    private static string Destination(UserRole role) => role switch
    {
        UserRole.Customer => "/shopper",
        UserRole.Creator => "/creator",
        UserRole.MerchantAdmin => "/business",
        UserRole.PlatformAdmin => "/admin",
        _ => "/"
    };
    private static UserRole MapRole(string role) => role switch
    {
        "Customer" => UserRole.Customer,
        "Creator" => UserRole.Creator,
        "Business" => UserRole.MerchantAdmin,
        "PlatformAdmin" => UserRole.PlatformAdmin,
        _ => throw new UnauthorizedAccessException("The Weymela profile is not supported.")
    };
    private static string V3Role(UserRole role) => role switch
    {
        UserRole.Customer => "Customer",
        UserRole.Creator => "Creator",
        UserRole.MerchantAdmin => "Business",
        UserRole.PlatformAdmin => "PlatformAdmin",
        UserRole.Cashier => "Cashier",
        _ => throw new UnauthorizedAccessException("The Weymela profile is not supported.")
    };
    private void Validate(V3IdentityAssertion assertion)
    {
        var now = clock.UtcNow;
        if (assertion.Issuer != options.Issuer || assertion.Audience != options.Audience
            || assertion.Environment != options.Environment || assertion.ExpiresAtUtc <= now
            || assertion.IssuedAtUtc > now.AddMinutes(1) || assertion.UserId == Guid.Empty
            || assertion.IdentityBindingId == Guid.Empty || assertion.DeviceAssurance != "V3_DEVICE_UNLOCKED")
            throw new UnauthorizedAccessException("The Weymela handoff is invalid or expired.");
    }
    private void EnsureEnabled()
    {
        if (!options.Enabled) throw new InvalidOperationException("V3 product integration is disabled.");
    }
    private static bool IsRetryable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException postgres && postgres.SqlState is
                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
                return true;
        return false;
    }
    private sealed record NormalizedSocialProfile(SocialPlatform Platform, string ProfileUrl,
        long AudienceCount);
    private static IReadOnlyList<NormalizedSocialProfile> NormalizeSocialProfiles(
        IReadOnlyList<ExternalCreatorSocialProfile>? profiles)
    {
        if (profiles is null || profiles.Count == 0)
            throw new ArgumentException("Add at least one social profile.");
        if (profiles.Count > 4 || profiles.GroupBy(x => x.Platform).Any(x => x.Count() > 1))
            throw new ArgumentException("Add each supported social platform at most once.");
        var normalized = new List<NormalizedSocialProfile>(profiles.Count);
        foreach (var profile in profiles)
        {
            if (profile.Platform is not (SocialPlatform.TikTok or SocialPlatform.Instagram
                    or SocialPlatform.YouTube or SocialPlatform.Facebook))
                throw new ArgumentException("Choose a supported social platform.");
            if (string.IsNullOrWhiteSpace(profile.ProfileUrl) || profile.AudienceCount is null)
                throw new ArgumentException("Complete both the profile URL and audience count.");
            if (profile.AudienceCount < 0)
                throw new ArgumentException("Audience count must be a non-negative integer.");
            if (!Uri.TryCreate(profile.ProfileUrl.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
                || !OfficialHost(profile.Platform, uri.Host) || uri.AbsolutePath is "" or "/")
                throw new ArgumentException($"Enter a valid {profile.Platform} profile URL.");
            var builder = new UriBuilder(uri)
            {
                Scheme = Uri.UriSchemeHttps, Host = uri.IdnHost.ToLowerInvariant(), Port = -1,
                Fragment = ""
            };
            var url = builder.Uri.AbsoluteUri.TrimEnd('/');
            if (url.Length > 500)
                throw new ArgumentException("Social profile URL is too long.");
            normalized.Add(new(profile.Platform, url, profile.AudienceCount.Value));
        }
        return normalized;
    }
    private static bool OfficialHost(SocialPlatform platform, string host)
    {
        static bool Is(string value, string expected) => value.Equals(expected,
            StringComparison.OrdinalIgnoreCase) || value.EndsWith('.' + expected,
            StringComparison.OrdinalIgnoreCase);
        return platform switch
        {
            SocialPlatform.TikTok => Is(host, "tiktok.com"),
            SocialPlatform.Instagram => Is(host, "instagram.com"),
            SocialPlatform.YouTube => Is(host, "youtube.com") || Is(host, "youtu.be"),
            SocialPlatform.Facebook => Is(host, "facebook.com") || Is(host, "fb.com"),
            _ => false
        };
    }
    private static ExternalSessionContext Context(ExternalApplicationSession session, ExternalIdentity identity,
        ExternalProfileLink profile, UserAccount? user, string? accountEmail = null,
        string? accountPhone = null) => new(new(session.Id, identity.Id, profile.Id,
            session.UserAccountId, session.Role, session.Purpose,
            session.Purpose == V3HandoffPurposes.ProfileOnboarding && session.UserAccountId is null,
            session.ExpiresAtUtc), identity, profile, user, accountEmail, accountPhone);
}
