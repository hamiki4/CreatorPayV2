using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Authentication;

public sealed class AuthenticationService(IAuthenticationStore store, IPasswordHasher passwords, ITokenService tokens, IUtcClock clock,
    IPasswordResetNotifier notifier, PasswordPolicyValidator policy, IOptions<JwtOptions> jwt, IOptions<LockoutOptions> lockout,
    IOptions<PasswordResetOptions> resetOptions) : IAuthenticationService
{
    private const string InvalidCredentials = "Invalid email or password.";
    public async Task<Result<TokenPair>> LoginAsync(LoginRequest request, RequestContext context, CancellationToken ct)
    {
        var email = Normalize(request.Email); var now = clock.UtcNow; var user = await store.FindUserByEmailAsync(email, ct);
        if (user is null) { Audit(null, email, false, "InvalidCredentials", context, now); await store.SaveAsync(ct); return Result<TokenPair>.Failure(InvalidCredentials); }
        var eligible = CanSignIn(user) && (user.LockoutEndUtc is null || user.LockoutEndUtc <= now);
        var verification = passwords.Verify(user, user.PasswordHash, request.Password);
        if (!eligible || verification == PasswordVerification.Failed)
        {
            user.FailedLoginCount++; user.LastFailedLoginAtUtc = now;
            if (user.FailedLoginCount >= lockout.Value.MaxFailedAttempts) user.LockoutEndUtc = now.AddMinutes(lockout.Value.LockoutMinutes);
            Audit(user.Id, email, false, user.LockoutEndUtc > now ? "LockedOrIneligible" : "InvalidCredentials", context, now);
            await store.SaveAsync(ct); return Result<TokenPair>.Failure(InvalidCredentials);
        }
        if (verification == PasswordVerification.SuccessRehashNeeded) user.PasswordHash = passwords.Hash(user, request.Password);
        user.FailedLoginCount = 0; user.LastFailedLoginAtUtc = null; user.LockoutEndUtc = null; user.LastLoginAtUtc = now;
        var pair = IssuePair(user, Guid.NewGuid().ToString("N"), context, now); Audit(user.Id, email, true, null, context, now);
        await store.SaveAsync(ct); return Result<TokenPair>.Success(pair);
    }

    public Task<Result<TokenPair>> RefreshAsync(string token, RequestContext context, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var now = clock.UtcNow; var hash = tokens.HashToken(token); var existing = await store.FindRefreshAsync(hash, innerCt);
        if (existing is null) return Result<TokenPair>.Failure("Invalid refresh token.");
        if (!existing.IsActive(now)) { await store.RevokeFamilyAsync(existing.TokenFamily, now, "Token reuse detected", context.IpAddress, innerCt); await store.SaveAsync(innerCt); return Result<TokenPair>.Failure("Invalid refresh token."); }
        var user = await store.FindUserAsync(existing.UserAccountId, innerCt);
        if (user is null || !CanSignIn(user) || user.LockoutEndUtc > now) { await store.RevokeFamilyAsync(existing.TokenFamily, now, "Account ineligible", context.IpAddress, innerCt); await store.SaveAsync(innerCt); return Result<TokenPair>.Failure("Invalid refresh token."); }
        existing.UsedAtUtc = now; existing.RevokedAtUtc = now; existing.RevokedReason = "Rotated"; existing.RevokedByIp = context.IpAddress;
        var pair = IssuePair(user, existing.TokenFamily, context, now); existing.ReplacedByTokenHash = tokens.HashToken(pair.RefreshToken);
        await store.SaveAsync(innerCt); return Result<TokenPair>.Success(pair);
    }, ct);

    public async Task<OperationResult> LogoutAsync(string token, RequestContext context, CancellationToken ct)
    {
        var item = await store.FindRefreshAsync(tokens.HashToken(token), ct); if (item is null || item.RevokedAtUtc is not null) return OperationResult.Success();
        item.RevokedAtUtc = clock.UtcNow; item.RevokedReason = "Logout"; item.RevokedByIp = context.IpAddress; await store.SaveAsync(ct); return OperationResult.Success();
    }
    public async Task<OperationResult> LogoutAllAsync(Guid userId, RequestContext context, CancellationToken ct) { await store.RevokeAllAsync(userId, clock.UtcNow, "Logout all", context.IpAddress, ct); await store.SaveAsync(ct); return OperationResult.Success(); }
    public async Task<Result<CurrentUser>> GetCurrentUserAsync(Guid userId, CancellationToken ct) { var u = await store.FindUserAsync(userId, ct); return u is null ? Result<CurrentUser>.Failure("User not found.") : Result<CurrentUser>.Success(ToCurrent(u)); }
    public async Task<OperationResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, RequestContext context, CancellationToken ct)
    {
        if (request.NewPassword != request.Confirmation) return OperationResult.Failure("Password confirmation does not match.");
        var errors = policy.Validate(request.NewPassword); if (errors.Count > 0) return OperationResult.Failure(string.Join(" ", errors));
        var user = await store.FindUserAsync(userId, ct); if (user is null || passwords.Verify(user, user.PasswordHash, request.CurrentPassword) == PasswordVerification.Failed) return OperationResult.Failure("Current password is invalid.");
        if (passwords.Verify(user, user.PasswordHash, request.NewPassword) != PasswordVerification.Failed) return OperationResult.Failure("New password must differ from the current password.");
        user.PasswordHash = passwords.Hash(user, request.NewPassword); user.UpdatedAtUtc = clock.UtcNow; await store.RevokeAllAsync(user.Id, clock.UtcNow, "Password changed", context.IpAddress, ct); await store.SaveAsync(ct); return OperationResult.Success();
    }
    public async Task ForgotPasswordAsync(string email, RequestContext context, CancellationToken ct)
    {
        var user = await store.FindUserByEmailAsync(Normalize(email), ct); if (user is null || user.Status != AccountStatus.Active) return;
        var raw = tokens.CreateOpaqueToken(); var now = clock.UtcNow; store.AddReset(new PasswordResetToken { Id = Guid.NewGuid(), UserAccountId = user.Id, TokenHash = tokens.HashToken(raw), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(resetOptions.Value.TokenLifetimeMinutes), RequestedByIp = context.IpAddress });
        await store.SaveAsync(ct); await notifier.NotifyAsync(user, raw, ct);
    }
    public Task<OperationResult> ResetPasswordAsync(ResetPasswordRequest request, RequestContext context, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        if (request.NewPassword != request.Confirmation) return OperationResult.Failure("Password confirmation does not match.");
        var errors = policy.Validate(request.NewPassword); if (errors.Count > 0) return OperationResult.Failure(string.Join(" ", errors));
        var item = await store.FindResetAsync(tokens.HashToken(request.ResetToken), innerCt); var now = clock.UtcNow;
        if (item is null || item.UsedAtUtc is not null || item.ExpiresAtUtc <= now) return OperationResult.Failure("Invalid or expired reset token.");
        var user = await store.FindUserAsync(item.UserAccountId, innerCt); if (user is null || user.Status != AccountStatus.Active) return OperationResult.Failure("Invalid or expired reset token.");
        item.UsedAtUtc = now; item.UsedByIp = context.IpAddress; user.PasswordHash = passwords.Hash(user, request.NewPassword); user.UpdatedAtUtc = now;
        await store.RevokeAllAsync(user.Id, now, "Password reset", context.IpAddress, innerCt); await store.SaveAsync(innerCt); return OperationResult.Success();
    }, ct);
    private TokenPair IssuePair(UserAccount user, string family, RequestContext context, DateTime now)
    {
        var access = tokens.CreateAccessToken(user); var raw = tokens.CreateOpaqueToken(); var expiry = now.AddDays(jwt.Value.RefreshTokenDays);
        store.AddRefresh(new RefreshToken { Id = Guid.NewGuid(), UserAccountId = user.Id, TokenHash = tokens.HashToken(raw), TokenFamily = family, CreatedAtUtc = now, ExpiresAtUtc = expiry, CreatedByIp = context.IpAddress, UserAgent = context.UserAgent });
        return new(access.Token, access.ExpiresAtUtc, raw, expiry, ToCurrent(user));
    }
    private void Audit(Guid? id, string email, bool success, string? reason, RequestContext c, DateTime now) => store.AddAudit(new LoginAudit { Id = Guid.NewGuid(), UserAccountId = id, NormalizedEmail = email, WasSuccessful = success, FailureReason = reason, IpAddress = c.IpAddress, UserAgent = c.UserAgent, CorrelationId = c.CorrelationId, AttemptedAtUtc = now, CreatedAtUtc = now });
    private static CurrentUser ToCurrent(UserAccount u) => new(u.Id, u.Email, u.Role, u.Status, u.CreatorId, u.MerchantId, u.SupervisorId, u.CashierId, u.IsEmailVerified, u.IsPhoneVerified);
    public static bool CanSignIn(UserAccount user) => user.Status == AccountStatus.Active ||
        user.Status is AccountStatus.PendingVerification or AccountStatus.PendingApproval && user.Role is UserRole.Customer or UserRole.Creator or UserRole.MerchantAdmin;
    private static string Normalize(string email) => email.Trim().ToUpperInvariant();
}
