using System.Security.Cryptography;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CreatorPay.Infrastructure.Authentication;

public sealed class DisabledSmsOtpSender : ISmsOtpSender
{
    public Task SendAsync(string normalizedPhoneNumber, string code, CancellationToken ct) =>
        throw new InvalidOperationException("SMS OTP provider is not configured.");
}

public sealed class PilotTestSmsOtpSender(IHostEnvironment environment) : ISmsOtpSender
{
    public Task SendAsync(string normalizedPhoneNumber, string code, CancellationToken ct)
    {
        if (!environment.IsEnvironment("Pilot") && !environment.IsEnvironment("E2E") && !environment.IsEnvironment("Test") && !environment.IsDevelopment())
            throw new InvalidOperationException("Test SMS OTP provider is forbidden in this environment.");
        // The code is intentionally not logged or returned to normal clients. Authorized E2E uses configured TestCode.
        return Task.CompletedTask;
    }
}

public sealed class PhoneOtpService(ApplicationDbContext db, ISmsOtpSender sender, IOptions<SmsOtpOptions> configured,
    IUtcClock clock, IPasswordHasher passwords, PasswordPolicyValidator passwordPolicy, IHostEnvironment environment) : IPhoneOtpService
{
    readonly SmsOtpOptions options = configured.Value;

    public async Task IssueAsync(UserAccount user, string purpose, string? requestedByIp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.NormalizedPhoneNumber)) throw new InvalidOperationException("Account has no phone identity.");
        var now = clock.UtcNow;
        var recent = await db.PhoneOtpChallenges.Where(x => x.UserAccountId == user.Id && x.Purpose == purpose && x.UsedAtUtc == null).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (recent is not null && recent.LastSentAtUtc.AddSeconds(options.ResendCooldownSeconds) > now) throw new InvalidOperationException("Please wait before requesting another code.");
        // Only one code per user/purpose may be usable. Issuing a replacement consumes every older challenge.
        await db.PhoneOtpChallenges.Where(x => x.UserAccountId == user.Id && x.Purpose == purpose && x.UsedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAtUtc, now), ct);
        var code = options.SmsProvider == "PilotTest" && options.TestCode.Length == 6 ? options.TestCode : RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var item = new PhoneOtpChallenge { Id = Guid.NewGuid(), UserAccountId = user.Id, Purpose = purpose, CodeHash = Hash(user.Id, purpose, code), ExpiresAtUtc = now.AddMinutes(options.OtpExpiryMinutes), LastSentAtUtc = now, RequestedByIp = requestedByIp, CreatedAtUtc = now };
        db.Add(item); Audit("PhoneOtpIssued", user, purpose, now); await db.SaveChangesAsync(ct);
        await sender.SendAsync(user.NormalizedPhoneNumber, code, ct);
        if (purpose == "Registration" && options.PilotRegistrationAutoVerifyEnabled)
        {
            if (!environment.IsEnvironment("Pilot") && !environment.IsEnvironment("E2E") && !environment.IsEnvironment("Test"))
                throw new InvalidOperationException("Pilot registration auto-verification is forbidden in this environment.");
            var result = await VerifyRegistrationAsync(user.NormalizedPhoneNumber, code, ct);
            if (!result.Succeeded) throw new InvalidOperationException("Pilot registration auto-verification failed.");
            Audit("PilotRegistrationAutoVerified", user, purpose, clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<PhoneOtpResult> ResendAsync(string phoneNumber, string purpose, string? requestedByIp, CancellationToken ct)
    {
        var user = await Find(phoneNumber, ct); if (user is null) return PhoneOtpResult.Success();
        try { await IssueAsync(user, purpose, requestedByIp, ct); return PhoneOtpResult.Success(); }
        catch (InvalidOperationException x) { return PhoneOtpResult.Failure(x.Message); }
    }

    public async Task<PhoneOtpResult> VerifyRegistrationAsync(string phoneNumber, string code, CancellationToken ct)
    {
        var user = await Find(phoneNumber, ct); if (user is null) return PhoneOtpResult.Failure("Invalid or expired verification code.");
        var verified = await Verify(user, "Registration", code, ct); if (!verified.Succeeded) return verified;
        var now = clock.UtcNow; user.IsPhoneVerified = true;
        if (user.Role == UserRole.Customer) user.Status = AccountStatus.Active;
        else if (user.Role is UserRole.Creator or UserRole.MerchantAdmin) user.Status = AccountStatus.PendingApproval;
        else if (user.Role == UserRole.Cashier) user.Status = AccountStatus.Active;
        if (user.CreatorId.HasValue) { var creator = await db.Creators.SingleAsync(x => x.Id == user.CreatorId, ct); creator.Status = CreatorStatus.PendingApproval; }
        if (user.CashierId.HasValue) { var cashier = await db.Cashiers.SingleAsync(x => x.Id == user.CashierId, ct); cashier.IsActive = true; }
        Audit("PhoneVerified", user, "Registration", now); await db.SaveChangesAsync(ct); return PhoneOtpResult.Success();
    }

    public async Task RequestPasswordResetAsync(string phoneNumber, string? requestedByIp, CancellationToken ct)
    {
        var user = await Find(phoneNumber, ct); if (user is null || user.Role == UserRole.PlatformAdmin) return;
        try { await IssueAsync(user, "PasswordReset", requestedByIp, ct); } catch (InvalidOperationException) { }
    }

    public async Task<PhoneOtpResult> ResetPasswordAsync(ResetPasswordPhoneRequest request, string? usedByIp, CancellationToken ct)
    {
        if (request.NewPassword != request.Confirmation) return PhoneOtpResult.Failure("Password confirmation does not match.");
        var errors = passwordPolicy.Validate(request.NewPassword); if (errors.Count > 0) return PhoneOtpResult.Failure(string.Join(" ", errors));
        var user = await Find(request.PhoneNumber, ct); if (user is null || user.Role == UserRole.PlatformAdmin) return PhoneOtpResult.Failure("Invalid or expired verification code.");
        var verified = await Verify(user, "PasswordReset", request.Code, ct); if (!verified.Succeeded) return verified;
        var now = clock.UtcNow; user.PasswordHash = passwords.Hash(user, request.NewPassword); user.UpdatedAtUtc = now;
        await db.RefreshTokens.Where(x => x.UserAccountId == user.Id && x.RevokedAtUtc == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now).SetProperty(x => x.RevokedReason, "Password reset").SetProperty(x => x.RevokedByIp, usedByIp), ct);
        Audit("PhonePasswordReset", user, "PasswordReset", now); await db.SaveChangesAsync(ct); return PhoneOtpResult.Success();
    }

    async Task<PhoneOtpResult> Verify(UserAccount user, string purpose, string code, CancellationToken ct)
    {
        var now = clock.UtcNow; var item = await db.PhoneOtpChallenges.Where(x => x.UserAccountId == user.Id && x.Purpose == purpose && x.UsedAtUtc == null).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (item is null || item.ExpiresAtUtc <= now || item.FailedAttempts >= options.MaxAttempts) return PhoneOtpResult.Failure("Invalid or expired verification code.");
        if (code.Length != 6 || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(item.CodeHash), Encoding.ASCII.GetBytes(Hash(user.Id, purpose, code))))
        { item.FailedAttempts++; Audit("PhoneOtpRejected", user, purpose, now); await db.SaveChangesAsync(ct); return PhoneOtpResult.Failure("Invalid or expired verification code."); }
        item.UsedAtUtc = now; return PhoneOtpResult.Success();
    }

    async Task<UserAccount?> Find(string phone, CancellationToken ct)
    { try { var normalized = EthiopianMobileNumber.Normalize(phone); return await db.UserAccounts.SingleOrDefaultAsync(x => x.NormalizedPhoneNumber == normalized, ct); } catch (ArgumentException) { return null; } }
    string Hash(Guid user, string purpose, string code)
    {
        if (options.HashSecret.Length < 32) throw new InvalidOperationException("SmsOtp:HashSecret must be at least 32 characters.");
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.HashSecret), Encoding.UTF8.GetBytes($"{user:N}:{purpose}:{code}")));
    }
    void Audit(string type, UserAccount user, string purpose, DateTime now) => db.OperationalAuditEvents.Add(new() { Id = Guid.NewGuid(), EventType = type, ActorUserId = user.Id, SubjectId = user.Id, MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { userId = user.Id, role = user.Role.ToString(), purpose, utcTimestamp = now }), CorrelationId = Guid.NewGuid().ToString("N"), CreatedAtUtc = now });
}
