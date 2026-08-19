using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Authentication;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class PhoneOtpPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("creatorpay_phone_otp_tests").WithUsername("creatorpay").WithPassword("test-only-password").Build();
    private readonly MutableClock clock = new() { Now = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc) };
    private readonly SmsOtpOptions otp = new()
    {
        SmsProvider = "PilotTest",
        TestCode = "123456",
        HashSecret = "phone-otp-test-secret-at-least-32-characters",
        OtpExpiryMinutes = 5,
        ResendCooldownSeconds = 60,
        MaxAttempts = 3
    };
    private readonly PasswordHasherService passwords = new();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var db = Db();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [DockerFact]
    public async Task Registration_otp_is_hashed_single_use_expiring_throttled_locked_and_superseded()
    {
        await using var db = Db();
        var user = await AddUser(db, "+251911000101", "Old-password-1!");
        var service = Service(db);

        await service.IssueAsync(user, "Registration", "127.0.0.1", default);
        var first = await db.PhoneOtpChallenges.SingleAsync();
        Assert.NotEqual("123456", first.CodeHash);
        Assert.DoesNotContain("123456", first.CodeHash, StringComparison.Ordinal);
        Assert.Equal(64, first.CodeHash.Length);
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "PhoneOtpIssued" && x.SubjectId == user.Id));

        var cooldown = await service.ResendAsync("0911000101", "Registration", null, default);
        Assert.False(cooldown.Succeeded);
        Assert.Contains("wait", cooldown.Error!, StringComparison.OrdinalIgnoreCase);

        Assert.False((await service.VerifyRegistrationAsync("0911000101", "000000", default)).Succeeded);
        Assert.Equal(1, (await db.PhoneOtpChallenges.SingleAsync()).FailedAttempts);
        Assert.True((await service.VerifyRegistrationAsync("+251911000101", "123456", default)).Succeeded);
        Assert.False((await service.VerifyRegistrationAsync("251911000101", "123456", default)).Succeeded);
        await db.Entry(user).ReloadAsync();
        Assert.True(user.IsPhoneVerified);
        Assert.Equal(AccountStatus.Active, user.Status);

        var expiredUser = await AddUser(db, "+251911000102", "Old-password-1!");
        await service.IssueAsync(expiredUser, "Registration", null, default);
        clock.Now = clock.Now.AddMinutes(6);
        Assert.False((await service.VerifyRegistrationAsync(expiredUser.NormalizedPhoneNumber!, "123456", default)).Succeeded);

        var lockedUser = await AddUser(db, "+251911000103", "Old-password-1!");
        await service.IssueAsync(lockedUser, "Registration", null, default);
        for (var attempt = 0; attempt < otp.MaxAttempts; attempt++)
            Assert.False((await service.VerifyRegistrationAsync(lockedUser.NormalizedPhoneNumber!, "000000", default)).Succeeded);
        Assert.False((await service.VerifyRegistrationAsync(lockedUser.NormalizedPhoneNumber!, "123456", default)).Succeeded);

        var replacedUser = await AddUser(db, "+251911000104", "Old-password-1!");
        otp.TestCode = "111111";
        await service.IssueAsync(replacedUser, "Registration", null, default);
        clock.Now = clock.Now.AddSeconds(61);
        otp.TestCode = "222222";
        Assert.True((await service.ResendAsync(replacedUser.NormalizedPhoneNumber!, "Registration", null, default)).Succeeded);
        Assert.False((await service.VerifyRegistrationAsync(replacedUser.NormalizedPhoneNumber!, "111111", default)).Succeeded);
        Assert.True((await service.VerifyRegistrationAsync(replacedUser.NormalizedPhoneNumber!, "222222", default)).Succeeded);
        var replacementChallenges = await db.PhoneOtpChallenges.AsNoTracking().Where(x => x.UserAccountId == replacedUser.Id).OrderBy(x => x.CreatedAtUtc).ToListAsync();
        Assert.NotNull(replacementChallenges[0].UsedAtUtc);
        Assert.NotNull(replacementChallenges[1].UsedAtUtc);
    }

    [DockerFact]
    public async Task Password_reset_otp_is_persisted_single_use_and_revokes_sessions_without_enumeration_state()
    {
        await using var db = Db();
        otp.TestCode = "654321";
        var user = await AddUser(db, "+251911000105", "Old-password-1!");
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserAccountId = user.Id,
            TokenHash = new string('A', 64),
            TokenFamily = "family",
            ExpiresAtUtc = clock.Now.AddDays(1),
            CreatedAtUtc = clock.Now
        });
        await db.SaveChangesAsync();
        var service = Service(db);

        await service.RequestPasswordResetAsync("0911000105", "127.0.0.1", default);
        Assert.True(await db.PhoneOtpChallenges.AnyAsync(x => x.UserAccountId == user.Id && x.Purpose == "PasswordReset"));
        Assert.False((await service.ResetPasswordAsync(new("0911000105", "000000", "New-password-2!", "New-password-2!"), null, default)).Succeeded);
        Assert.True((await service.ResetPasswordAsync(new("+251911000105", "654321", "New-password-2!", "New-password-2!"), "127.0.0.1", default)).Succeeded);
        Assert.False((await service.ResetPasswordAsync(new("251911000105", "654321", "Another-password-3!", "Another-password-3!"), null, default)).Succeeded);
        await db.Entry(user).ReloadAsync();
        Assert.Equal(PasswordVerification.Failed, passwords.Verify(user, user.PasswordHash, "Old-password-1!"));
        Assert.NotEqual(PasswordVerification.Failed, passwords.Verify(user, user.PasswordHash, "New-password-2!"));
        Assert.NotNull((await db.RefreshTokens.AsNoTracking().SingleAsync()).RevokedAtUtc);
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "PhonePasswordReset" && x.SubjectId == user.Id));

        var before = await db.PhoneOtpChallenges.CountAsync();
        await service.RequestPasswordResetAsync("+251911999999", null, default);
        Assert.Equal(before, await db.PhoneOtpChallenges.CountAsync());
    }

    [DockerFact]
    public async Task Pilot_registration_auto_verify_consumes_hashed_challenge_but_password_reset_still_requires_otp()
    {
        await using var db = Db();
        otp.PilotRegistrationAutoVerifyEnabled = true;
        var user = await AddUser(db, "+251911000106", "Old-password-1!");
        var service = Service(db);
        await service.IssueAsync(user, "Registration", null, default);
        await db.Entry(user).ReloadAsync();
        var registration = await db.PhoneOtpChallenges.SingleAsync(x => x.UserAccountId == user.Id && x.Purpose == "Registration");
        Assert.True(user.IsPhoneVerified);
        Assert.Equal(AccountStatus.Active, user.Status);
        Assert.NotNull(registration.UsedAtUtc);
        Assert.NotEqual(otp.TestCode, registration.CodeHash);
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "PilotRegistrationAutoVerified" && x.SubjectId == user.Id));
        await service.RequestPasswordResetAsync(user.NormalizedPhoneNumber!, null, default);
        var reset = await db.PhoneOtpChallenges.SingleAsync(x => x.UserAccountId == user.Id && x.Purpose == "PasswordReset");
        Assert.Null(reset.UsedAtUtc);
        Assert.False((await service.ResetPasswordAsync(new(user.NormalizedPhoneNumber!, "000000", "New-password-2!", "New-password-2!"), null, default)).Succeeded);
    }

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(container.GetConnectionString()).Options);

    private PhoneOtpService Service(ApplicationDbContext db) => new(db, new CapturingSender(), Options.Create(otp), clock, passwords,
        new PasswordPolicyValidator(Options.Create(new PasswordOptions())), new EnvironmentStub());

    private async Task<UserAccount> AddUser(ApplicationDbContext db, string phone, string password)
    {
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = string.Empty,
            NormalizedEmail = string.Empty,
            PhoneNumber = phone,
            NormalizedPhoneNumber = phone,
            Role = UserRole.Customer,
            Status = AccountStatus.PendingVerification,
            CreatedAtUtc = clock.Now
        };
        user.PasswordHash = passwords.Hash(user, password);
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class CapturingSender : ISmsOtpSender
    {
        public Task SendAsync(string normalizedPhoneNumber, string code, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class MutableClock : IUtcClock
    {
        public DateTime Now { get; set; }
        public DateTime UtcNow => Now;
    }

    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "E2E";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
