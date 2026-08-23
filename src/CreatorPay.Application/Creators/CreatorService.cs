using System.ComponentModel.DataAnnotations;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Creators;

public sealed class CreatorService(ICreatorStore store, IPasswordHasher passwords, ITokenService tokens, IUtcClock clock,
    ICreatorVerificationProvider provider, PasswordPolicyValidator passwordPolicy, IOptions<CreatorVerificationOptions> options, IPhoneOtpService phoneOtp,
    ICreatorProfilePhotoStore profilePhotos) : ICreatorService
{
    public Task<CreatorResult<CreatorRegistrationResponse>> RegisterAsync(RegisterCreatorRequest request, CancellationToken ct)
    {
        _ = phoneOtp; // retained for constructor compatibility while OTP registration is disabled
        _ = provider; // legacy verification endpoints remain compatible but are not part of onboarding
        var error = ValidateIdentity(request.FirstName, request.LastName, request.DisplayName, request.Email, request.PhoneNumber);
        if (error is not null) return Task.FromResult(CreatorResult<CreatorRegistrationResponse>.Failure(error));
        error = ValidatePilotProfile(request.TermsAccepted, request.City, request.Biography, request.ContentCategories, request.SocialProfiles);
        if (error is not null) return Task.FromResult(CreatorResult<CreatorRegistrationResponse>.Failure(error));
        var passwordErrors = passwordPolicy.Validate(request.Password);
        if (passwordErrors.Count > 0) return Task.FromResult(CreatorResult<CreatorRegistrationResponse>.Failure(string.Join(" ", passwordErrors)));
        if (request.Confirmation is not null && request.Password != request.Confirmation) return Task.FromResult(CreatorResult<CreatorRegistrationResponse>.Failure("Password confirmation does not match."));
        return RegisterValidatedAsync(request, ct);
    }

    private Task<CreatorResult<CreatorRegistrationResponse>> RegisterValidatedAsync(RegisterCreatorRequest request, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var registrationEmail = request.Email?.Trim() ?? string.Empty;
        var normalizedEmail = NormalizeEmail(registrationEmail); var normalizedPhone = NormalizePhone(request.PhoneNumber);
        if (normalizedEmail.Length > 0 && await store.EmailExistsAsync(normalizedEmail, null, innerCt)) return CreatorResult<CreatorRegistrationResponse>.Failure("Email is already registered.");
        if (await store.PhoneExistsAsync(normalizedPhone, null, innerCt)) return CreatorResult<CreatorRegistrationResponse>.Failure("Phone number is already registered.");
        var now = clock.UtcNow; var creatorId = Guid.NewGuid(); var userId = Guid.NewGuid(); var creatorCode = await store.AllocateCreatorCodeAsync(innerCt);
        var creator = new Creator { Id = creatorId, PublicCreatorId = $"CR-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), CreatorCode = creatorCode, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DisplayName = request.DisplayName.Trim(), PhoneNumber = FormatPhone(normalizedPhone), NormalizedPhoneNumber = normalizedPhone, Email = registrationEmail, PreferredLanguage = request.PreferredLanguage.Trim(), City = request.City.Trim(), Zone = request.Zone?.Trim(), Biography = request.Biography.Trim(), ContentCategories = request.ContentCategories.Trim(), GovernmentIdReference = request.GovernmentIdReference?.Trim(), TaxIdentificationNumber = request.TaxIdentificationNumber?.Trim(), PreferredPayoutChannel = request.PreferredPayoutChannel?.Trim(), PreferredPayoutAccountIdentifier = request.PreferredPayoutAccountIdentifier?.Trim(), TermsAcceptedAtUtc = now, Status = CreatorStatus.PendingApproval, CreatedAtUtc = now, CreatedBy = userId.ToString() };
        var user = new UserAccount { Id = userId, Email = registrationEmail, NormalizedEmail = normalizedEmail, PhoneNumber = normalizedPhone, NormalizedPhoneNumber = normalizedPhone, Role = UserRole.Creator, Status = AccountStatus.PendingApproval, CreatorId = creatorId, IsEmailVerified = false, IsPhoneVerified = false, CreatedAtUtc = now, CreatedBy = userId.ToString() };
        user.PasswordHash = passwords.Hash(user, request.Password); store.Add(creator); store.Add(user); AddSocialProfiles(creatorId, request.SocialProfiles!, now, userId);
        Audit(creatorId, userId, "Registration", null, now);
        await store.SaveAsync(innerCt);
        return CreatorResult<CreatorRegistrationResponse>.Success(new(creatorId, creator.PublicCreatorId, "Content Creator registration received. Your account is pending Platform review.", false));
    }, ct);

    public Task<CreatorResult> VerifyEmailAsync(string token, CancellationToken ct) => VerifyAsync(token, "Email", ct);
    public Task<CreatorResult> VerifyPhoneAsync(string token, CancellationToken ct) => VerifyAsync(token, "Phone", ct);

    private Task<CreatorResult> VerifyAsync(string raw, string purpose, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        if (string.IsNullOrWhiteSpace(raw)) return CreatorResult.Failure("Invalid or expired verification token.");
        var hash = tokens.HashToken(raw); var item = await store.FindTokenAsync(hash, purpose, innerCt); var now = clock.UtcNow;
        if (item is null || item.UsedAtUtc is not null || item.ExpiresAtUtc <= now) return CreatorResult.Failure("Invalid or expired verification token.");
        var pair = await store.FindByTokenAsync(hash, purpose, innerCt);
        if (pair is null) return CreatorResult.Failure("Invalid or expired verification token.");
        item.UsedAtUtc = now;
        if (purpose == "Email") pair.Value.User.IsEmailVerified = true; else pair.Value.User.IsPhoneVerified = true;
        if (pair.Value.Creator.Status == CreatorStatus.Draft) pair.Value.Creator.Status = CreatorStatus.PendingVerification;
        if (pair.Value.User.IsEmailVerified && pair.Value.User.IsPhoneVerified)
        {
            pair.Value.User.Status = AccountStatus.PendingApproval; pair.Value.Creator.Status = CreatorStatus.PendingApproval;
        }
        Audit(pair.Value.Creator.Id, pair.Value.User.Id, $"{purpose}Verification", null, now); await store.SaveAsync(innerCt);
        return CreatorResult.Success();
    }, ct);

    public async Task<CreatorResult<CreatorProfileResponse>> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var pair = await store.FindByUserAsync(userId, ct);
        return pair is null ? CreatorResult<CreatorProfileResponse>.Failure("Creator not found.") : CreatorResult<CreatorProfileResponse>.Success(ToProfile(pair.Value.User, pair.Value.Creator));
    }

    public Task<CreatorResult<CreatorProfileResponse>> UpdateMeAsync(Guid userId, UpdateCreatorProfileRequest request, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var pair = await store.FindByUserAsync(userId, innerCt); if (pair is null) return CreatorResult<CreatorProfileResponse>.Failure("Creator not found.");
        var error = ValidateIdentity(request.FirstName, request.LastName, request.DisplayName, request.Email, request.PhoneNumber);
        if (error is not null) return CreatorResult<CreatorProfileResponse>.Failure(error);
        error = ValidatePilotProfile(true, request.City, request.Biography, request.ContentCategories, request.SocialProfiles);
        if (error is not null) return CreatorResult<CreatorProfileResponse>.Failure(error);
        if (request.ProfileImage is { } image && (image.SizeBytes is <= 0 or > ProfilePhotoLimits.MaximumUploadBytes || image.FileName.Trim().Length is 0 or > 255 || image.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))) return CreatorResult<CreatorProfileResponse>.Failure("Profile image metadata is invalid.");
        var normalizedEmail = NormalizeEmail(request.Email); var normalizedPhone = NormalizePhone(request.PhoneNumber);
        if (await store.EmailExistsAsync(normalizedEmail, userId, innerCt)) return CreatorResult<CreatorProfileResponse>.Failure("Email is already registered.");
        if (await store.PhoneExistsAsync(normalizedPhone, pair.Value.Creator.Id, innerCt)) return CreatorResult<CreatorProfileResponse>.Failure("Phone number is already registered.");
        var now = clock.UtcNow; var emailChanged = pair.Value.User.NormalizedEmail != normalizedEmail; var phoneChanged = pair.Value.Creator.NormalizedPhoneNumber != normalizedPhone;
        pair.Value.Creator.FirstName = request.FirstName.Trim(); pair.Value.Creator.LastName = request.LastName.Trim(); pair.Value.Creator.DisplayName = request.DisplayName.Trim(); pair.Value.Creator.Email = request.Email.Trim(); pair.Value.Creator.PhoneNumber = FormatPhone(normalizedPhone); pair.Value.Creator.NormalizedPhoneNumber = normalizedPhone;
        pair.Value.Creator.PreferredLanguage = request.PreferredLanguage.Trim(); pair.Value.Creator.City = request.City.Trim(); pair.Value.Creator.Zone = request.Zone?.Trim(); pair.Value.Creator.Biography = request.Biography.Trim(); pair.Value.Creator.ContentCategories = request.ContentCategories.Trim(); pair.Value.Creator.PreferredPayoutChannel = request.PreferredPayoutChannel?.Trim(); pair.Value.Creator.PreferredPayoutAccountIdentifier = request.PreferredPayoutAccountIdentifier?.Trim();
        store.RemoveSocialProfiles(pair.Value.Creator.SocialProfiles.ToArray()); AddSocialProfiles(pair.Value.Creator.Id, request.SocialProfiles!, now, userId);
        pair.Value.User.Email = request.Email.Trim(); pair.Value.User.NormalizedEmail = normalizedEmail; pair.Value.User.PhoneNumber = normalizedPhone; pair.Value.User.NormalizedPhoneNumber = normalizedPhone; pair.Value.Creator.UpdatedAtUtc = now; pair.Value.Creator.UpdatedBy = userId.ToString(); pair.Value.User.UpdatedAtUtc = now; pair.Value.User.UpdatedBy = userId.ToString();
        if (request.ProfileImage is { } metadata) { pair.Value.Creator.ProfileImageFileName = metadata.FileName.Trim(); pair.Value.Creator.ProfileImageContentType = metadata.ContentType; pair.Value.Creator.ProfileImageSizeBytes = metadata.SizeBytes; }
        Audit(pair.Value.Creator.Id, userId, "ProfileUpdate", $"EmailChanged={emailChanged};PhoneChanged={phoneChanged}", now); await store.SaveAsync(innerCt);
        return CreatorResult<CreatorProfileResponse>.Success(ToProfile(pair.Value.User, pair.Value.Creator));
    }, ct);

    public Task<CreatorResult<CreatorProfileResponse>> UploadProfilePhotoAsync(Guid userId, Stream content, string contentType, long sizeBytes, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var pair = await store.FindByUserAsync(userId, innerCt); if (pair is null) return CreatorResult<CreatorProfileResponse>.Failure("Creator not found.");
        if (!IsValidPhoto(contentType, sizeBytes)) return CreatorResult<CreatorProfileResponse>.Failure("Profile photo is invalid.");
        var now = clock.UtcNow;
        var oldKey = pair.Value.Creator.ProfileImageFileName;
        (string StorageKey, long SizeBytes) stored;
        try { stored = await profilePhotos.SaveAsync(pair.Value.Creator.Id, content, contentType, sizeBytes, innerCt); }
        catch (Exception ex) { return CreatorResult<CreatorProfileResponse>.Failure(ex.Message.StartsWith("Profile photo", StringComparison.OrdinalIgnoreCase) ? ex.Message : "Profile photo is invalid."); }
        var storageKey = stored.StorageKey;
        pair.Value.Creator.ProfileImageFileName = storageKey;
        pair.Value.Creator.ProfileImageContentType = contentType.Trim().ToLowerInvariant();
        pair.Value.Creator.ProfileImageSizeBytes = stored.SizeBytes;
        pair.Value.Creator.UpdatedAtUtc = now;
        pair.Value.Creator.UpdatedBy = userId.ToString();
        Audit(pair.Value.Creator.Id, userId, oldKey is null ? "ProfilePhotoUploaded" : "ProfilePhotoReplaced", null, now);
        await store.SaveAsync(innerCt);
        if (!string.IsNullOrWhiteSpace(oldKey) && !string.Equals(oldKey, storageKey, StringComparison.Ordinal))
        {
            try { await profilePhotos.DeleteAsync(oldKey, ct); } catch { /* preserve profile state if cleanup fails */ }
        }
        return CreatorResult<CreatorProfileResponse>.Success(ToProfile(pair.Value.User, pair.Value.Creator));
    }, ct);

    public Task<CreatorResult<CreatorProfileResponse>> RemoveProfilePhotoAsync(Guid userId, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var pair = await store.FindByUserAsync(userId, innerCt); if (pair is null) return CreatorResult<CreatorProfileResponse>.Failure("Creator not found.");
        if (pair.Value.Creator.ProfileImageFileName is not { Length: > 0 } oldKey) return CreatorResult<CreatorProfileResponse>.Success(ToProfile(pair.Value.User, pair.Value.Creator));
        var now = clock.UtcNow;
        pair.Value.Creator.ProfileImageFileName = null;
        pair.Value.Creator.ProfileImageContentType = null;
        pair.Value.Creator.ProfileImageSizeBytes = null;
        pair.Value.Creator.UpdatedAtUtc = now;
        pair.Value.Creator.UpdatedBy = userId.ToString();
        Audit(pair.Value.Creator.Id, userId, "ProfilePhotoRemoved", null, now);
        await store.SaveAsync(innerCt);
        try { await profilePhotos.DeleteAsync(oldKey, ct); } catch { /* keep profile state canonical even if cleanup fails */ }
        return CreatorResult<CreatorProfileResponse>.Success(ToProfile(pair.Value.User, pair.Value.Creator));
    }, ct);

    public async Task<IReadOnlyList<PendingCreatorResponse>> GetPendingAsync(CancellationToken ct) => (await store.FindByStatusAsync(CreatorStatus.PendingApproval, ct)).Select(x => new PendingCreatorResponse(x.Id, x.PublicCreatorId, x.DisplayName, x.Email, x.CreatedAtUtc)).ToArray();
    public async Task<CreatorResult<CreatorProfileResponse>> GetAsync(Guid creatorId, CancellationToken ct) { var c = await store.FindCreatorAsync(creatorId, ct); if (c is null) return CreatorResult<CreatorProfileResponse>.Failure("Creator not found."); var u = await FindCreatorUser(c, ct); return u is null ? CreatorResult<CreatorProfileResponse>.Failure("Creator not found.") : CreatorResult<CreatorProfileResponse>.Success(ToProfile(u, c)); }
    public Task<CreatorResult> ApproveAsync(Guid id, Guid admin, CancellationToken ct) => DecideAsync(id, admin, null, "Approval", (c, now, actor) => c.Approve(now, actor), CreatorStatus.PendingApproval, AccountStatus.Active, ct);
    public Task<CreatorResult> RejectAsync(Guid id, Guid admin, string? reason, CancellationToken ct) => DecideAsync(id, admin, reason, "Rejection", (c, now, actor) => c.Reject(now, actor.ToString()), CreatorStatus.PendingApproval, AccountStatus.Rejected, ct, true);
    public Task<CreatorResult> RequestCorrectionAsync(Guid id, Guid admin, string? reason, CancellationToken ct) => DecideAsync(id, admin, reason, "CorrectionRequested", (c, now, actor) => c.RequestCorrection(now, actor.ToString()), CreatorStatus.PendingApproval, AccountStatus.PendingApproval, ct, true);
    public Task<CreatorResult> SuspendAsync(Guid id, Guid admin, string? reason, CancellationToken ct) => DecideAsync(id, admin, reason, "Suspension", (c, now, actor) => c.Suspend(now, actor.ToString()), CreatorStatus.Active, AccountStatus.Suspended, ct, true);
    public Task<CreatorResult> ReactivateAsync(Guid id, Guid admin, string? reason, CancellationToken ct) => DecideAsync(id, admin, reason, "Reactivation", (c, now, actor) => c.Reactivate(now, actor.ToString()), CreatorStatus.Suspended, AccountStatus.Active, ct);

    private Task<CreatorResult> DecideAsync(Guid id, Guid admin, string? reason, string eventType, Action<Creator, DateTime, Guid> transition, CreatorStatus required, AccountStatus accountStatus, CancellationToken ct, bool reasonRequired = false) => store.InTransactionAsync(async innerCt =>
    {
        if (reasonRequired && string.IsNullOrWhiteSpace(reason)) return CreatorResult.Failure("A reason is required.");
        var creator = await store.FindCreatorAsync(id, innerCt); if (creator is null) return CreatorResult.Failure("Creator not found.");
        if (creator.Status != required) return CreatorResult.Failure($"Creator must be {required} for this action.");
        var user = await FindCreatorUser(creator, innerCt); if (user is null) return CreatorResult.Failure("Creator account not found.");
        // Contact details are not authentication factors; Platform review controls approval.
        var now = clock.UtcNow; transition(creator, now, admin); user.Status = accountStatus; user.UpdatedAtUtc = now; user.UpdatedBy = admin.ToString(); Audit(id, admin, eventType, reason?.Trim(), now); await store.SaveAsync(innerCt); return CreatorResult.Success();
    }, ct);

    private Task<UserAccount?> FindCreatorUser(Creator creator, CancellationToken ct) => store.FindUserByCreatorAsync(creator.Id, ct);
    private (CreatorVerificationToken Item, string Raw) Issue(Guid userId, string purpose, DateTime now) { var raw = tokens.CreateOpaqueToken(); return (new CreatorVerificationToken { Id = Guid.NewGuid(), UserAccountId = userId, Purpose = purpose, TokenHash = tokens.HashToken(raw), ExpiresAtUtc = now.AddMinutes(options.Value.TokenLifetimeMinutes), CreatedAtUtc = now }, raw); }
    private void Audit(Guid creatorId, Guid? actor, string type, string? detail, DateTime now) => store.Add(new CreatorAuditEvent { Id = Guid.NewGuid(), CreatorId = creatorId, ActorUserAccountId = actor, EventType = type, Detail = detail, CreatedAtUtc = now, CreatedBy = actor?.ToString() });
    private static string? ValidateIdentity(string first, string last, string display, string? email, string phone) { if (string.IsNullOrWhiteSpace(first) || first.Trim().Length > 100) return "First name is required and must not exceed 100 characters."; if (string.IsNullOrWhiteSpace(last) || last.Trim().Length > 100) return "Last name is required and must not exceed 100 characters."; if (string.IsNullOrWhiteSpace(display) || display.Trim().Length is < 2 or > 200) return "Display name must be between 2 and 200 characters."; if (!string.IsNullOrWhiteSpace(email) && (!new EmailAddressAttribute().IsValid(email.Trim()) || email.Trim().Length > 320)) return "Email is invalid."; if (!EthiopianMobileNumber.TryNormalize(phone, out _)) return EthiopianMobileNumber.ValidationMessage; return null; }
    internal static string NormalizeEmail(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
    internal static string NormalizePhone(string value) => EthiopianMobileNumber.Normalize(value);
    private static string FormatPhone(string normalized) => normalized;
    private static CreatorProfileResponse ToProfile(UserAccount user, Creator c) => new(c.Id, c.PublicCreatorId, c.CreatorCode, c.FirstName, c.LastName, c.DisplayName, c.PhoneNumber, user.Email, user.IsEmailVerified, user.IsPhoneVerified, user.Status, c.Status, c.ProfileImageFileName is null ? null : new(c.ProfileImageFileName, c.ProfileImageContentType!, c.ProfileImageSizeBytes!.Value), Next(user, c), c.PreferredLanguage, c.City, c.Zone, c.Biography, c.ContentCategories, c.SocialProfiles.Select(x => new SocialProfileResponse(x.Id, x.Platform, x.Handle, x.ProfileUrl, x.FollowerCount, x.IsPrimary, x.VerificationStatus, x.CreatedAtUtc, x.UpdatedAtUtc)).ToArray(), c.PreferredPayoutChannel, c.PreferredPayoutAccountIdentifier);
    private static string Next(UserAccount u, Creator c) => c.Status == CreatorStatus.PendingApproval ? "Your profile is awaiting platform approval." : c.Status == CreatorStatus.Active ? "Your creator account is active." : c.Status == CreatorStatus.Rejected ? "Your application was rejected. Contact platform support." : c.Status == CreatorStatus.Suspended ? "Your creator account is suspended. Contact platform support." : "Complete creator onboarding.";
    private static string? ValidatePilotProfile(bool terms, string city, string bio, string categories, IReadOnlyList<SocialProfileRequest>? profiles)
    {
        if (!terms) return "Terms acceptance is required."; if (string.IsNullOrWhiteSpace(city) || city.Trim().Length > 120) return "Primary city is required.";
        if (bio.Trim().Length > 1000) return "Biography must not exceed 1000 characters.";
        if (categories.Trim().Length > 500) return "Content categories must not exceed 500 characters.";
        if (profiles is null || profiles.Count == 0 || profiles.Count(x => x.IsPrimary) != 1) return "Exactly one primary social profile is required.";
        if (profiles.Any(x => (string.IsNullOrWhiteSpace(x.Handle) && string.IsNullOrWhiteSpace(x.ProfileUrl)) || SocialKey(x).Length > 200 || x.FollowerCount < 0)) return "Social profile details are invalid.";
        if (profiles.GroupBy(x => new { x.Platform, Handle = SocialKey(x) }).Any(x => x.Count() > 1)) return "Duplicate social platform and handle combinations are not allowed.";
        return null;
    }
    private void AddSocialProfiles(Guid creatorId, IReadOnlyList<SocialProfileRequest> profiles, DateTime now, Guid actor)
    { foreach (var x in profiles) store.Add(new CreatorSocialProfile { Id = Guid.NewGuid(), CreatorId = creatorId, Platform = x.Platform, Handle = SocialKey(x), ProfileUrl = x.ProfileUrl?.Trim(), FollowerCount = x.FollowerCount, IsPrimary = x.IsPrimary, VerificationStatus = SocialProfileVerificationStatus.Unverified, CreatedAtUtc = now, CreatedBy = actor.ToString() }); }
    private static string SocialKey(SocialProfileRequest profile) => string.IsNullOrWhiteSpace(profile.Handle) ? profile.ProfileUrl!.Trim().ToUpperInvariant() : profile.Handle.Trim().ToUpperInvariant();
    private static bool IsValidPhoto(string contentType, long sizeBytes) => sizeBytes is > 0 and <= 5_000_000;
}
