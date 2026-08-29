using CreatorPay.Application.Accounts;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Application.CustomerVerification;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Authentication;

public sealed class AuthenticationService(IAuthenticationStore store, IPasswordHasher passwords, ITokenService tokens, IUtcClock clock,
    IPasswordResetNotifier notifier, PasswordPolicyValidator policy, IOptions<JwtOptions> jwt, IOptions<LockoutOptions> lockout,
    IOptions<PasswordResetOptions> resetOptions, IFirebaseIdentityVerifier firebase) : IAuthenticationService
{
    private const string InvalidEmailCredentials = "Invalid email or password.";
    private const string InvalidPhoneCredentials = "Invalid phone number or password.";
    public async Task<Result<TokenPair>> LoginAsync(LoginRequest request, RequestContext context, CancellationToken ct)
    {
        var identifier = request.Email.Trim(); var invalidCredentials = identifier.Contains('@') ? InvalidEmailCredentials : InvalidPhoneCredentials; var now = clock.UtcNow; UserAccount? user;
        if (identifier.Contains('@')) user = await store.FindUserByEmailAsync(Normalize(identifier), ct);
        else { try { user = await store.FindUserByPhoneAsync(EthiopianMobileNumber.Normalize(identifier), ct); } catch (ArgumentException) { user = null; } }
        var email = user?.NormalizedPhoneNumber ?? NormalizeLogin(identifier);
        if (user is null) { Audit(null, email, false, "InvalidCredentials", context, now); await store.SaveAsync(ct); return Result<TokenPair>.Failure(invalidCredentials); }
        var verification = passwords.Verify(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerification.Failed)
        {
            user.FailedLoginCount++; user.LastFailedLoginAtUtc = now;
            if (user.FailedLoginCount >= lockout.Value.MaxFailedAttempts) user.LockoutEndUtc = now.AddMinutes(lockout.Value.LockoutMinutes);
            Audit(user.Id, email, false, user.LockoutEndUtc > now ? "LockedOrIneligible" : "InvalidCredentials", context, now);
            await store.SaveAsync(ct); return Result<TokenPair>.Failure(invalidCredentials);
        }
        var restriction = GetRestriction(user, now);
        if (restriction is not null)
        {
            Audit(user.Id, email, false, restriction.Code, context, now);
            await store.SaveAsync(ct);
            return Result<TokenPair>.Failure(restriction.Message, restriction.Code);
        }
        if (!CanSignIn(user))
        {
            Audit(user.Id, email, false, "InvalidCredentials", context, now);
            await store.SaveAsync(ct); return Result<TokenPair>.Failure(invalidCredentials);
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

    public async Task<Result<PinStatus>> GetPinStatusAsync(Guid userId, CancellationToken ct)
    {
        var user = await store.FindUserAsync(userId, ct);
        if (user is null) return Result<PinStatus>.Failure("User not found.");
        var eligible = IsPinRole(user.Role);
        return Result<PinStatus>.Success(new(eligible, user.PinHash is not null, user.PinLockedAtUtc is not null, user.PinFailedAttemptCount));
    }

    public async Task<OperationResult> LinkFirebaseAsync(Guid userId, LinkFirebaseRequest request, RequestContext context, CancellationToken ct)
    {
        var user = await store.FindUserAsync(userId, ct);
        if (user is null || !IsPinRole(user.Role)) return OperationResult.Failure("PIN enrollment is not available for this account.");
        var proofResult = await firebase.VerifyIdTokenAsync(request.FirebaseIdToken, true, ct);
        if (!proofResult.Succeeded) return OperationResult.Failure(proofResult.Error!);
        var proof = proofResult.Value!;
        if (!proof.EmailVerified) return OperationResult.Failure("Firebase email must be verified before it can be linked.");
        var requestedEmail = Normalize(request.RecoveryEmail);
        if (Normalize(proof.Email) != requestedEmail) return OperationResult.Failure("Firebase verified email does not match the requested recovery email.");
        var byUid = await store.FindUserByFirebaseUidAsync(proof.Uid, ct);
        var byEmail = await store.FindUserByRecoveryEmailAsync(requestedEmail, ct);
        if ((byUid is not null && byUid.Id != user.Id) || (byEmail is not null && byEmail.Id != user.Id))
            return OperationResult.Failure("This Firebase identity or recovery email is already linked to another account.");
        if (user.FirebaseUid is not null && !string.Equals(user.FirebaseUid, proof.Uid, StringComparison.Ordinal))
            return OperationResult.Failure("This account is already linked to a different Firebase identity.");
        user.FirebaseUid = proof.Uid; user.RecoveryEmail = proof.Email.Trim(); user.NormalizedRecoveryEmail = requestedEmail;
        user.IsRecoveryEmailVerified = true; user.UpdatedAtUtc = clock.UtcNow;
        Audit(user.Id, requestedEmail, true, "FirebaseIdentityLinked", context, clock.UtcNow);
        await store.SaveAsync(ct); return OperationResult.Success();
    }

    public async Task<OperationResult> EnrollPinAsync(Guid userId, PinRequest request, RequestContext context, CancellationToken ct)
    {
        var user = await store.FindUserAsync(userId, ct);
        if (user is null || !IsPinRole(user.Role)) return OperationResult.Failure("PIN enrollment is not available for this account.");
        if (user.PinHash is not null) return OperationResult.Failure("A PIN is already enrolled. Use Forgot PIN to replace it.");
        if (!ValidPin(request.Pin) || request.Pin != request.Confirmation) return OperationResult.Failure("PIN must contain exactly 5 numeric digits and match confirmation.");
        var now = clock.UtcNow; user.PinHash = passwords.Hash(user, PinCredential(request.Pin)); user.PinVersion = 1;
        user.PinEnrolledAtUtc = now; user.PinChangedAtUtc = now; user.UpdatedAtUtc = now;
        Audit(user.Id, user.NormalizedPhoneNumber ?? user.NormalizedEmail, true, "PinEnrolled", context, now);
        await store.SaveAsync(ct); return OperationResult.Success();
    }

    public Task<Result<TokenPair>> UnlockWithPinAsync(PinUnlockRequest request, RequestContext context, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var now = clock.UtcNow; string normalized;
        try { normalized = EthiopianMobileNumber.Normalize(request.PhoneNumber); } catch (ArgumentException) { normalized = string.Empty; }
        var user = normalized.Length == 0 ? null : await store.FindUserByPhoneAsync(normalized, innerCt);
        if (user is null || !IsPinRole(user.Role) || user.PinHash is null)
        { Audit(user?.Id, normalized, false, "PinInvalid", context, now); await store.SaveAsync(innerCt); return Result<TokenPair>.Failure("Invalid phone number or PIN."); }
        if (user.PinLockedAtUtc is not null)
        { Audit(user.Id, normalized, false, "PinLocked", context, now); await store.SaveAsync(innerCt); return Result<TokenPair>.Failure(PinLockedMessage); }
        if (user.PinRetryNotBeforeUtc > now) return Result<TokenPair>.Failure("Please wait before trying the PIN again.");
        var valid = ValidPin(request.Pin) && passwords.Verify(user, user.PinHash, PinCredential(request.Pin)) != PasswordVerification.Failed;
        if (!valid)
        {
            user.PinFailedAttemptCount++; user.PinRetryNotBeforeUtc = now.AddSeconds(Math.Min(30, Math.Pow(2, Math.Min(5, user.PinFailedAttemptCount - 1))));
            if (user.PinFailedAttemptCount >= 10) { user.PinFailedAttemptCount = 10; user.PinLockedAtUtc = now; user.PinRetryNotBeforeUtc = null; }
            Audit(user.Id, normalized, false, user.PinLockedAtUtc is null ? "PinInvalid" : "PinLockedAtTenFailures", context, now);
            await store.SaveAsync(innerCt); return Result<TokenPair>.Failure(user.PinLockedAtUtc is null ? "Invalid phone number or PIN." : PinLockedMessage);
        }
        var restriction = GetRestriction(user, now);
        if (restriction is not null)
        {
            Audit(user.Id, normalized, false, restriction.Code, context, now);
            await store.SaveAsync(innerCt);
            return Result<TokenPair>.Failure(restriction.Message, restriction.Code);
        }
        if (!CanSignIn(user))
        {
            Audit(user.Id, normalized, false, "PinInvalid", context, now);
            await store.SaveAsync(innerCt);
            return Result<TokenPair>.Failure("Invalid phone number or PIN.");
        }
        user.PinRetryNotBeforeUtc = null; user.LastLoginAtUtc = now;
        var pair = IssuePair(user, Guid.NewGuid().ToString("N"), context, now);
        Audit(user.Id, normalized, true, "PinUnlock", context, now); await store.SaveAsync(innerCt);
        return Result<TokenPair>.Success(pair);
    }, ct);

    public Task<OperationResult> ResetPinWithPasswordAsync(PasswordPinResetRequest request, RequestContext context, CancellationToken ct) => store.InTransactionAsync(async innerCt =>
    {
        var now = clock.UtcNow;
        string normalized;
        try { normalized = EthiopianMobileNumber.Normalize(request.PhoneNumber); } catch (ArgumentException) { normalized = string.Empty; }
        var user = normalized.Length == 0 ? null : await store.FindUserByPhoneAsync(normalized, innerCt);
        var verified = user is not null && IsPinRole(user.Role) && CanSignIn(user) &&
            (user.LockoutEndUtc is null || user.LockoutEndUtc <= now) &&
            passwords.Verify(user, user.PasswordHash, request.Password) != PasswordVerification.Failed;
        if (!verified)
        {
            if (user is not null && IsPinRole(user.Role))
            {
                user.FailedLoginCount++; user.LastFailedLoginAtUtc = now;
                if (user.FailedLoginCount >= lockout.Value.MaxFailedAttempts) user.LockoutEndUtc = now.AddMinutes(lockout.Value.LockoutMinutes);
            }
            Audit(user?.Id, normalized, false, "PinPasswordRecoveryRejected", context, now);
            await store.SaveAsync(innerCt); return OperationResult.Failure("Invalid phone number or password.");
        }
        if (!ValidPin(request.NewPin) || request.NewPin != request.Confirmation)
            return OperationResult.Failure("PIN must contain exactly 5 numeric digits and match confirmation.");
        user!.PinHash = passwords.Hash(user, PinCredential(request.NewPin)); user.PinVersion++;
        user.PinChangedAtUtc = now; user.PinEnrolledAtUtc ??= now; user.PinFailedAttemptCount = 0;
        user.PinLockedAtUtc = null; user.PinRetryNotBeforeUtc = null; user.FailedLoginCount = 0;
        user.LastFailedLoginAtUtc = null; user.LockoutEndUtc = null; user.UpdatedAtUtc = now;
        await store.RevokeAllAsync(user.Id, now, "PIN reset", context.IpAddress, innerCt);
        Audit(user.Id, normalized, true, "PinPasswordRecoveryCompleted", context, now);
        await store.SaveAsync(innerCt); return OperationResult.Success();
    }, ct);
    private TokenPair IssuePair(UserAccount user, string family, RequestContext context, DateTime now)
    {
        var access = tokens.CreateAccessToken(user); var raw = tokens.CreateOpaqueToken(); var expiry = now.AddDays(jwt.Value.RefreshTokenDays);
        store.AddRefresh(new RefreshToken { Id = Guid.NewGuid(), UserAccountId = user.Id, TokenHash = tokens.HashToken(raw), TokenFamily = family, CreatedAtUtc = now, ExpiresAtUtc = expiry, CreatedByIp = context.IpAddress, UserAgent = context.UserAgent });
        return new(access.Token, access.ExpiresAtUtc, raw, expiry, ToCurrent(user));
    }
    private void Audit(Guid? id, string email, bool success, string? reason, RequestContext c, DateTime now) => store.AddAudit(new LoginAudit { Id = Guid.NewGuid(), UserAccountId = id, NormalizedEmail = email, WasSuccessful = success, FailureReason = reason, IpAddress = c.IpAddress, UserAgent = c.UserAgent, CorrelationId = c.CorrelationId, AttemptedAtUtc = now, CreatedAtUtc = now });
    private CurrentUser ToCurrent(UserAccount u)
    {
        var effective = EffectiveAccountStatus.FromAccount(u, clock.UtcNow);
        return new(u.Id, u.Email, u.PhoneNumber, u.Role, u.Status, effective.EffectiveStatus, effective.EffectiveStatusReason, u.CreatorId, u.MerchantId, u.SupervisorId, u.CashierId, u.IsEmailVerified, u.IsPhoneVerified);
    }
    public static bool CanSignIn(UserAccount user) => user.Status == AccountStatus.Active ||
        user.Status is AccountStatus.PendingVerification or AccountStatus.PendingApproval && IsPinRole(user.Role);
    private static RestrictedAccount? GetRestriction(UserAccount user, DateTime now) => user.LockoutEndUtc is not null && user.LockoutEndUtc > now
        ? new("AccountLocked", "Your account is locked. Please contact Weymela support.")
        : user.Status switch
        {
            AccountStatus.Suspended => new("AccountSuspended", "Your account is suspended. Please contact Weymela support."),
            AccountStatus.Closed or AccountStatus.Rejected => new("AccountDeactivated", "Your account is deactivated. Please contact Weymela support."),
            _ => null
        };
    private static string Normalize(string email) => email.Trim().ToUpperInvariant();
    private const string PinLockedMessage = "Too many incorrect attempts. Use Forgot PIN to reset your PIN.";
    private static bool IsPinRole(UserRole role) => role is UserRole.Customer or UserRole.Creator or UserRole.MerchantAdmin or UserRole.Cashier;
    private static bool ValidPin(string pin) => pin.Length == 5 && pin.All(char.IsAsciiDigit);
    private static string PinCredential(string pin) => $"weymela-pin-v1:{pin}";
    private sealed record RestrictedAccount(string Code, string Message);
    private static string NormalizeLogin(string identifier) { var value = identifier.Trim().ToUpperInvariant(); return value.Contains('@') ? value : $"{value}@CASHIER.WEYMELA.LOCAL"; }
}
public sealed class FirebasePinOptions
{
    public const string SectionName = "FirebaseAuth";
    public int PinResetAuthorizationMinutes { get; set; } = 10;
}
