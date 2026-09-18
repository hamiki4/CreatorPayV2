using CreatorPay.Application.Authentication;
using CreatorPay.Application.Integration;
using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Integration;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class V3ExternalIntegrationTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 9, 17, 3, 0, 0, DateTimeKind.Utc);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("creatorpay_v3_integration").WithUsername("creatorpay")
        .WithPassword("test-only-password").Build();

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await using var db = Db();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Customer_onboarding_is_idempotent_uses_one_external_identity_and_never_creates_local_credentials()
    {
        var authority = new FakeAuthority();
        var assertion = authority.Queue(Code("customer-onboarding"), "Customer", V3HandoffPurposes.ProfileOnboarding);
        Guid sessionId;
        await using (var db = Db())
        {
            var context = await Service(db, authority).RedeemAsync(Code("customer-onboarding"), "integration-callback", default);
            sessionId = context.Session.SessionId;
            Assert.True(context.Session.IsOnboarding);
        }

        ExternalProvisioningResult first;
        await using (var db = Db())
            first = await Service(db, authority).ProvisionCustomerAsync(sessionId,
                new ExternalCustomerRegistration("Mimi"), default);
        await using (var db = Db())
        {
            var replay = await Service(db, authority).ProvisionCustomerAsync(sessionId,
                new ExternalCustomerRegistration("Ignored on replay"), default);
            Assert.Equal(first.UserAccountId, replay.UserAccountId);
            Assert.Equal(first.ProfileId, replay.ProfileId);
        }

        await using var verify = Db();
        Assert.Single(await verify.ExternalIdentities.Where(x => x.ExternalUserId == assertion.UserId).ToListAsync());
        Assert.Single(await verify.ExternalProfileLinks.ToListAsync());
        Assert.Single(await verify.Customers.ToListAsync());
        var user = await verify.UserAccounts.SingleAsync();
        Assert.Equal(AuthenticationSource.V3External, user.AuthenticationSource);
        Assert.Empty(user.PasswordHash);
        Assert.Null(user.PinHash);
        Assert.Null(user.FirebaseUid);
        Assert.False(AuthenticationService.CanSignIn(user));
        Assert.Single(authority.Synchronizations);
        Assert.Equal("ACTIVE", authority.Synchronizations.Single().Lifecycle);
    }

    [Fact]
    public async Task Existing_V3_customer_maps_once_without_recreating_the_V3_profile()
    {
        var authority = new FakeAuthority();
        var existingSubject = Guid.NewGuid();
        var firstAssertion = authority.Queue(Code("existing-customer-a"), "Customer",
            V3HandoffPurposes.ExistingWorkspace, existingSubject);
        authority.Queue(Code("existing-customer-b"), "Customer",
            V3HandoffPurposes.ExistingWorkspace, existingSubject, firstAssertion.UserId,
            firstAssertion.IdentityBindingId);

        await using (var db = Db())
        {
            var first = await Service(db, authority).RedeemAsync(Code("existing-customer-a"), "integration-callback", default);
            Assert.Equal(UserRole.Customer, first.Session.Role);
            Assert.False(first.Session.IsOnboarding);
        }
        await using (var db = Db())
            _ = await Service(db, authority).RedeemAsync(Code("existing-customer-b"), "integration-callback", default);

        await using var verify = Db();
        Assert.Single(await verify.ExternalIdentities.ToListAsync());
        var link = Assert.Single(await verify.ExternalProfileLinks.ToListAsync());
        Assert.Equal(existingSubject, link.ExternalProfileSubjectId);
        Assert.Single(await verify.Customers.ToListAsync());
        Assert.Empty(authority.Synchronizations);
    }

    [Fact]
    public async Task Concurrent_customer_provisioning_is_retry_safe_and_creates_one_principal()
    {
        var authority = new FakeAuthority();
        authority.Queue(Code("concurrent-customer"), "Customer", V3HandoffPurposes.ProfileOnboarding);
        Guid sessionId;
        await using (var db = Db())
            sessionId = (await Service(db, authority)
                .RedeemAsync(Code("concurrent-customer"), "integration-callback", default)).Session.SessionId;

        async Task<ExternalProvisioningResult> Provision()
        {
            await using var db = Db();
            return await Service(db, authority).ProvisionCustomerAsync(sessionId,
                new ExternalCustomerRegistration("Concurrent Customer"), default);
        }

        var results = await Task.WhenAll(Provision(), Provision());
        Assert.Equal(results[0].UserAccountId, results[1].UserAccountId);
        Assert.Equal(results[0].ProfileId, results[1].ProfileId);
        await using var verify = Db();
        Assert.Single(await verify.Customers.ToListAsync());
        Assert.Single(await verify.UserAccounts.ToListAsync());
        Assert.Single(await verify.ExternalProfileLinks.ToListAsync());
    }

    [Fact]
    public async Task Pending_creator_handoff_resumes_the_same_mapping_without_repeating_registration()
    {
        var authority = new FakeAuthority();
        var first = authority.Queue(Code("creator-pending-a"), "Creator", V3HandoffPurposes.ProfileOnboarding);
        authority.Queue(Code("creator-pending-b"), "Creator", V3HandoffPurposes.ProfileOnboarding,
            userId: first.UserId, bindingId: first.IdentityBindingId);
        Guid sessionId;
        Guid? firstLink;
        await using (var db = Db())
        {
            var session = await Service(db, authority)
                .RedeemAsync(Code("creator-pending-a"), "integration-callback", default);
            sessionId = session.Session.SessionId;
            firstLink = session.Session.ProfileLinkId;
        }
        await using (var db = Db())
            _ = await Service(db, authority).ProvisionCreatorAsync(sessionId,
                new ExternalCreatorRegistration("Mimi", "Abebe", "Mimi Creates", "Addis Ababa",
                    null, "", "Food", [new(SocialPlatform.TikTok,
                        "https://www.tiktok.com/@mimi", 1000)]), default);
        await using (var db = Db())
        {
            var resumed = await Service(db, authority)
                .RedeemAsync(Code("creator-pending-b"), "integration-callback", default);
            Assert.Equal(firstLink, resumed.Session.ProfileLinkId);
            Assert.False(resumed.Session.IsOnboarding);
        }
        await using var verify = Db();
        Assert.Single(await verify.ExternalProfileLinks.ToListAsync());
        Assert.Single(await verify.Creators.ToListAsync());
        Assert.Single(await verify.UserAccounts.ToListAsync());
    }

    [Fact]
    public async Task One_V3_identity_supports_distinct_customer_creator_and_business_principals_with_pending_isolation()
    {
        var authority = new FakeAuthority();
        var user = Guid.NewGuid();
        var binding = Guid.NewGuid();
        authority.Queue(Code("customer"), "Customer", V3HandoffPurposes.ProfileOnboarding, userId: user, bindingId: binding);
        authority.Queue(Code("creator"), "Creator", V3HandoffPurposes.ProfileOnboarding, userId: user, bindingId: binding);
        authority.Queue(Code("business"), "Business", V3HandoffPurposes.ProfileOnboarding, userId: user, bindingId: binding);

        Guid customerSession;
        Guid creatorSession;
        Guid businessSession;
        await using (var db = Db()) customerSession = (await Service(db, authority)
            .RedeemAsync(Code("customer"), "integration-callback", default)).Session.SessionId;
        await using (var db = Db()) creatorSession = (await Service(db, authority)
            .RedeemAsync(Code("creator"), "integration-callback", default)).Session.SessionId;
        await using (var db = Db()) businessSession = (await Service(db, authority)
            .RedeemAsync(Code("business"), "integration-callback", default)).Session.SessionId;

        await using (var db = Db()) _ = await Service(db, authority).ProvisionCustomerAsync(customerSession,
            new ExternalCustomerRegistration("Mimi"), default);
        await using (var db = Db()) _ = await Service(db, authority).ProvisionCreatorAsync(creatorSession,
            new ExternalCreatorRegistration("Mimi", "Abebe", "Mimi Creates", "Addis Ababa",
                null, "", "Food", [new(SocialPlatform.TikTok,
                    "https://www.tiktok.com/@mimi", 1000)]), default);
        await using (var db = Db()) _ = await Service(db, authority).ProvisionBusinessAsync(businessSession,
            new ExternalBusinessRegistration("Mimi Cafe", BusinessTypes.Values[0], "Mimi", "0922222222",
                null, "Bole Road", "Addis Ababa", "Addis Ababa", "Ethiopia", "Africa/Addis_Ababa"), default);

        await using var verify = Db();
        Assert.Single(await verify.ExternalIdentities.ToListAsync());
        Assert.Equal(3, await verify.ExternalProfileLinks.CountAsync());
        Assert.Equal(3, await verify.UserAccounts.CountAsync());
        Assert.All(await verify.UserAccounts.ToListAsync(), x => Assert.Equal(AuthenticationSource.V3External, x.AuthenticationSource));
        Assert.Equal(AccountStatus.PendingApproval, (await verify.UserAccounts.SingleAsync(x => x.Role == UserRole.Creator)).Status);
        Assert.Equal(AccountStatus.PendingApproval, (await verify.UserAccounts.SingleAsync(x => x.Role == UserRole.MerchantAdmin)).Status);
        Assert.Contains(authority.Synchronizations, x => x.Role == "Creator" && x.Lifecycle == "PENDING");
        Assert.Contains(authority.Synchronizations, x => x.Role == "Business" && x.Lifecycle == "PENDING");
    }

    [Fact]
    public async Task One_V3_identity_can_provision_multiple_businesses_sequentially()
    {
        var authority = new FakeAuthority();
        var userId = Guid.NewGuid();
        var bindingId = Guid.NewGuid();
        authority.Queue(Code("business-one"), "Business", V3HandoffPurposes.ProfileOnboarding,
            userId: userId, bindingId: bindingId);
        authority.Queue(Code("business-two"), "Business", V3HandoffPurposes.ProfileOnboarding,
            userId: userId, bindingId: bindingId);

        ExternalProvisioningResult first;
        await using (var db = Db())
        {
            var handoff = await Service(db, authority)
                .RedeemAsync(Code("business-one"), "integration-callback", default);
            first = await Service(db, authority).ProvisionBusinessAsync(handoff.Session.SessionId,
                Business("First Business", "0911111111"), default);
        }
        await using (var db = Db())
        {
            var account = await db.UserAccounts.SingleAsync(x => x.MerchantId == first.ProfileId);
            account.Status = AccountStatus.Active;
            await db.SaveChangesAsync();
            Assert.True(await Service(db, authority).SynchronizeLifecycleAsync(
                UserRole.MerchantAdmin, first.ProfileId, "ACTIVE", default));
        }
        await using (var db = Db())
        {
            var handoff = await Service(db, authority)
                .RedeemAsync(Code("business-two"), "integration-callback", default);
            var second = await Service(db, authority).ProvisionBusinessAsync(handoff.Session.SessionId,
                Business("Second Business", "0922222222"), default);
            Assert.NotEqual(first.ProfileId, second.ProfileId);
        }

        await using var verify = Db();
        Assert.Single(await verify.ExternalIdentities.ToListAsync());
        Assert.Equal(2, await verify.ExternalProfileLinks.CountAsync(x => x.Role == UserRole.MerchantAdmin));
        Assert.Equal(2, await verify.Merchants.CountAsync());
        Assert.Equal(2, await verify.UserAccounts.CountAsync(x => x.Role == UserRole.MerchantAdmin));
        var keys = await verify.ExternalProfileLinks.Select(x => x.ProvisioningKey).ToListAsync();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Creator_onboarding_requires_unique_complete_supported_socials_and_active_edit_keeps_one()
    {
        var authority = new FakeAuthority();
        authority.Queue(Code("creator-socials"), "Creator", V3HandoffPurposes.ProfileOnboarding);
        Guid sessionId;
        await using (var db = Db())
        {
            var context = await Service(db, authority).RedeemAsync(Code("creator-socials"),
                "integration-callback", default);
            sessionId = context.Session.SessionId;
            Assert.Equal("mimi@example.test", context.AccountEmail);
            Assert.Equal("+251911111111", context.AccountPhone);
        }

        static ExternalCreatorRegistration Creator(params ExternalCreatorSocialProfile[] profiles) =>
            new("Mimi", "Abebe", "Mimi Creates", "Addis Ababa", null, "Food creator",
                "Food", profiles);

        await using (var db = Db())
        {
            var service = Service(db, authority);
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProvisionCreatorAsync(sessionId,
                Creator(), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProvisionCreatorAsync(sessionId,
                Creator(new ExternalCreatorSocialProfile(SocialPlatform.TikTok,
                    "https://www.tiktok.com/@mimi", null)), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProvisionCreatorAsync(sessionId,
                Creator(new ExternalCreatorSocialProfile(SocialPlatform.TikTok,
                    null, 10)), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProvisionCreatorAsync(sessionId,
                Creator(new ExternalCreatorSocialProfile(SocialPlatform.TikTok,
                    "https://www.tiktok.com/@mimi", -1)), default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ProvisionCreatorAsync(sessionId,
                Creator(new ExternalCreatorSocialProfile(SocialPlatform.TikTok,
                        "https://www.tiktok.com/@mimi", 10),
                    new ExternalCreatorSocialProfile(SocialPlatform.TikTok,
                        "https://www.tiktok.com/@mimi-two", 20)), default));
        }

        await using (var db = Db())
            _ = await Service(db, authority).ProvisionCreatorAsync(sessionId, Creator(
                new ExternalCreatorSocialProfile(SocialPlatform.TikTok,
                    "https://WWW.TIKTOK.COM/@mimi/", 10),
                new ExternalCreatorSocialProfile(SocialPlatform.Instagram,
                    "https://instagram.com/mimi", 20),
                new ExternalCreatorSocialProfile(SocialPlatform.YouTube,
                    "https://youtube.com/@mimi", 30),
                new ExternalCreatorSocialProfile(SocialPlatform.Facebook,
                    "https://facebook.com/mimi", 40)), default);

        await using (var db = Db())
        {
            var profiles = await db.CreatorSocialProfiles.OrderBy(x => x.FollowerCount).ToListAsync();
            Assert.Equal(4, profiles.Count);
            Assert.Equal("https://www.tiktok.com/@mimi", profiles[0].ProfileUrl);
            Assert.All(profiles, x => Assert.Equal(SocialProfileVerificationStatus.Unverified,
                x.VerificationStatus));
            Assert.Single(profiles, x => x.IsPrimary);
            var creator = await db.Creators.Include(x => x.SocialProfiles).SingleAsync();
            var user = await db.UserAccounts.SingleAsync(x => x.CreatorId == creator.Id);
            var link = await db.ExternalProfileLinks.SingleAsync(x => x.UserAccountId == user.Id);
            creator.Approve(Now, user.Id); user.Status = AccountStatus.Active;
            link.Status = ExternalProfileStatus.Active;
            await db.SaveChangesAsync();
            Assert.Empty(user.Email); Assert.True(string.IsNullOrEmpty(user.PhoneNumber));
            Assert.Empty(creator.PhoneNumber);
        }

        await using (var db = Db())
        {
            var service = Service(db, authority);
            await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateCreatorSocialProfilesAsync(
                sessionId, new([]), default));
            var updated = await service.UpdateCreatorSocialProfilesAsync(sessionId, new([
                new(SocialPlatform.YouTube, "https://youtube.com/@mimi-updated", 55),
                new(SocialPlatform.Facebook, "https://facebook.com/mimi-updated", 65)
            ]), default);
            Assert.Equal(2, updated.Count);
        }
        await using (var db = Db())
        {
            var profiles = await db.CreatorSocialProfiles.OrderBy(x => x.Platform).ToListAsync();
            Assert.Equal(2, profiles.Count);
            Assert.Contains(profiles, x => x.Platform == SocialPlatform.YouTube && x.FollowerCount == 55);
            Assert.Contains(profiles, x => x.Platform == SocialPlatform.Facebook && x.FollowerCount == 65);
        }
    }

    [Fact]
    public async Task Platform_admin_handoff_binds_only_the_configured_existing_admin_and_disables_local_authentication()
    {
        var authority = new FakeAuthority();
        var v3UserId = Guid.NewGuid();
        authority.Queue(Code("platform-admin"), "PlatformAdmin", V3HandoffPurposes.ExistingWorkspace,
            v3UserId, v3UserId, Guid.NewGuid());
        var configuredAdminId = Guid.NewGuid();
        await using (var db = Db())
        {
            db.UserAccounts.Add(new UserAccount
            {
                Id = configuredAdminId,
                Email = "existing-admin@e2e.invalid",
                NormalizedEmail = "EXISTING-ADMIN@E2E.INVALID",
                PasswordHash = "legacy-password-hash",
                FirebaseUid = "legacy-firebase-uid",
                PinHash = "legacy-pin-hash",
                AuthenticationSource = AuthenticationSource.Local,
                Role = UserRole.PlatformAdmin,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                CreatedAtUtc = Now
            });
            await db.SaveChangesAsync();
        }

        await using (var db = Db())
        {
            var context = await Service(db, authority, configuredAdminId)
                .RedeemAsync(Code("platform-admin"), "integration-callback", default);
            Assert.Equal(UserRole.PlatformAdmin, context.Session.Role);
            Assert.Equal(configuredAdminId, context.Session.UserAccountId);
        }

        await using var verify = Db();
        var admin = await verify.UserAccounts.SingleAsync();
        Assert.Equal(AuthenticationSource.V3External, admin.AuthenticationSource);
        Assert.Empty(admin.PasswordHash);
        Assert.Null(admin.FirebaseUid);
        Assert.Null(admin.PinHash);
        Assert.False(AuthenticationService.CanSignIn(admin));
        var link = await verify.ExternalProfileLinks.SingleAsync();
        Assert.Equal(configuredAdminId, link.UserAccountId);
        Assert.Equal(v3UserId, link.ExternalProfileSubjectId);
        Assert.Single(await verify.UserAccounts.Where(x => x.Role == UserRole.PlatformAdmin).ToListAsync());
    }

    [Fact]
    public async Task Operations_admin_handoff_creates_only_an_external_shadow_principal_and_reuses_it()
    {
        var authority = new FakeAuthority(); var user = Guid.NewGuid(); var binding = Guid.NewGuid();
        authority.Queue(Code("operations-admin-a"), "OperationsAdmin", V3HandoffPurposes.ExistingWorkspace,
            user, user, binding);
        authority.Queue(Code("operations-admin-b"), "OperationsAdmin", V3HandoffPurposes.ExistingWorkspace,
            user, user, binding);
        Guid firstAccount;
        await using (var db = Db())
        {
            var first = await Service(db, authority).RedeemAsync(Code("operations-admin-a"), "integration-callback", default);
            firstAccount = first.Session.UserAccountId!.Value;
            Assert.Equal("OperationsAdmin profile", first.User!.DisplayName);
        }
        await using (var db = Db())
        {
            var second = await Service(db, authority).RedeemAsync(Code("operations-admin-b"), "integration-callback", default);
            Assert.Equal(firstAccount, second.Session.UserAccountId);
        }
        await using var verify = Db(); var account = await verify.UserAccounts.SingleAsync();
        Assert.Equal(UserRole.OperationsAdmin, account.Role);
        Assert.Equal(AuthenticationSource.V3External, account.AuthenticationSource);
        Assert.Empty(account.PasswordHash); Assert.Null(account.PinHash); Assert.Null(account.FirebaseUid);
        Assert.Single(await verify.ExternalProfileLinks.ToListAsync());
    }

    [Fact]
    public async Task Correction_requested_lifecycle_is_synchronized_to_V3_without_activating_profile()
    {
        var authority = new FakeAuthority();
        authority.Queue(Code("creator-correction"), "Creator", V3HandoffPurposes.ProfileOnboarding);
        Guid session;
        await using (var db = Db()) session = (await Service(db, authority)
            .RedeemAsync(Code("creator-correction"), "integration-callback", default)).Session.SessionId;
        ExternalProvisioningResult profile;
        await using (var db = Db()) profile = await Service(db, authority).ProvisionCreatorAsync(session,
            new ExternalCreatorRegistration("Mimi", "Kibru", "Mimi Creates", "Addis Ababa", null,
                "Creator", "Food", [new(SocialPlatform.TikTok, "https://www.tiktok.com/@mimi", 1000)]), default);
        await using (var db = Db()) Assert.True(await Service(db, authority).SynchronizeLifecycleAsync(
            UserRole.Creator, profile.ProfileId, "CORRECTION_REQUESTED", default));
        Assert.Contains(authority.Synchronizations, x => x.ExternalSubjectId == profile.ProfileId
            && x.Lifecycle == "CORRECTION_REQUESTED");
        await using var verify = Db();
        Assert.Equal(ExternalProfileStatus.Pending, (await verify.ExternalProfileLinks.SingleAsync()).Status);
        Assert.Equal(AccountStatus.PendingApproval, (await verify.UserAccounts.SingleAsync()).Status);
    }

    private static ExternalBusinessRegistration Business(string name, string phone) => new(name,
        BusinessTypes.Values[0], name + " Owner", phone, null, "Bole Road", "Addis Ababa",
        "Addis Ababa", "Ethiopia", "Africa/Addis_Ababa");

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(database.GetConnectionString()).Options);

    private static string Code(string label) => $"phase-i1-{label}".PadRight(48, 'x');

    private static ExternalIntegrationService Service(ApplicationDbContext db, FakeAuthority authority,
        Guid? platformAdminUserAccountId = null) =>
        new(db, authority, Options(platformAdminUserAccountId), new FixedClock());

    private static V3IntegrationOptions Options(Guid? platformAdminUserAccountId = null) => new()
    {
        Enabled = true,
        Issuer = "weymela-v3-test",
        Audience = "creatorpay-test",
        Environment = "Test",
        V3ApiUrl = "https://v3-api.test/",
        V3WebUrl = "https://v3.test/",
        ProductWebUrl = "https://product.test/",
        CallbackId = "integration-callback",
        ClientId = "integration-test-client",
        ClientSecret = new string('s', 48),
        PlatformAdminUserAccountId = platformAdminUserAccountId,
        SessionLifetime = TimeSpan.FromMinutes(30),
        RevalidationInterval = TimeSpan.FromMinutes(5)
    };

    private sealed class FixedClock : IUtcClock { public DateTime UtcNow => Now; }

    private sealed class FakeAuthority : IV3AuthorityClient
    {
        private readonly Dictionary<string, V3IdentityAssertion> assertions = [];
        public List<V3ProfileSynchronizationRequest> Synchronizations { get; } = [];

        public V3IdentityAssertion Queue(string code, string role, string purpose, Guid? subject = null,
            Guid? userId = null, Guid? bindingId = null)
        {
            var assertion = new V3IdentityAssertion("weymela-v3-test", "Test", "creatorpay-test",
                userId ?? Guid.NewGuid(), bindingId ?? Guid.NewGuid(), 1, Now, purpose, role, subject,
                role == "Business" ? subject : null, role + " profile", "Active", "V3_DEVICE_UNLOCKED",
                "mimi@example.test", "+251911111111", Now, Now.AddMinutes(5));
            assertions.Add(code, assertion);
            return assertion;
        }

        public Task<V3IdentityAssertion> RedeemAsync(string code, string callbackId, CancellationToken ct) =>
            Task.FromResult(assertions[code]);

        public Task<V3AuthorityResult> RevalidateAsync(V3AuthorityRequest request, CancellationToken ct) =>
            Task.FromResult(new V3AuthorityResult(true, "Active", request.IdentityBindingVersion));

        public Task<V3ProfileSynchronizationResult> SynchronizeAsync(V3ProfileSynchronizationRequest request,
            CancellationToken ct)
        {
            bool created;
            lock (Synchronizations)
            {
                Synchronizations.Add(request);
                created = Synchronizations.Count(x => x.IdempotencyKey == request.IdempotencyKey) == 1;
            }
            return Task.FromResult(new V3ProfileSynchronizationResult(request.ExternalSubjectId,
                request.Lifecycle, created));
        }
    }
}
