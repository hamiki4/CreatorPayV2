using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Tests;

public sealed class AuthenticationServiceTests
{
    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Creator)]
    [InlineData(UserRole.MerchantAdmin)]
    [InlineData(UserRole.PlatformAdmin)]
    [InlineData(UserRole.Supervisor)]
    [InlineData(UserRole.Cashier)]
    public async Task Active_role_can_sign_in_with_the_correct_role(UserRole role)
    {
        var user = User(role, AccountStatus.Active); var service = Service(new FakeStore(user));
        var result = await service.LoginAsync(new(user.Email, "Correct1!"), Context(), default);
        Assert.True(result.Succeeded); Assert.Equal(role, result.Value!.User.Role); Assert.NotEmpty(result.Value.AccessToken); Assert.NotEmpty(result.Value.RefreshToken);
    }

    [Theory]
    [InlineData(UserRole.Creator, AccountStatus.PendingVerification)]
    [InlineData(UserRole.Creator, AccountStatus.PendingApproval)]
    [InlineData(UserRole.MerchantAdmin, AccountStatus.PendingVerification)]
    [InlineData(UserRole.MerchantAdmin, AccountStatus.PendingApproval)]
    public async Task Public_pending_account_can_sign_in_for_onboarding(UserRole role, AccountStatus status)
    {
        var user = User(role, status); user.IsPhoneVerified = true; var result = await Service(new FakeStore(user)).LoginAsync(new(user.Email, "Correct1!"), Context(), default);
        Assert.True(result.Succeeded); Assert.Equal(status, result.Value!.User.Status);
    }

    [Fact]
    public async Task Existing_shopper_pending_verification_can_sign_in_without_otp()
    {
        var user = User(UserRole.Customer, AccountStatus.PendingVerification); var result = await Service(new FakeStore(user)).LoginAsync(new(user.Email, "Correct1!"), Context(), default);
        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Rejected)]
    [InlineData(AccountStatus.Closed)]
    [InlineData(AccountStatus.Draft)]
    public async Task Ineligible_account_receives_generic_invalid_credentials(AccountStatus status)
    {
        var user = User(UserRole.Creator, status); var result = await Service(new FakeStore(user)).LoginAsync(new(user.Email, "Correct1!"), Context(), default);
        Assert.False(result.Succeeded); Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task Locked_account_receives_generic_invalid_credentials()
    {
        var user = User(UserRole.Customer, AccountStatus.Active); user.LockoutEndUtc = Now.AddMinutes(5);
        var result = await Service(new FakeStore(user)).LoginAsync(new(user.Email, "Correct1!"), Context(), default);
        Assert.False(result.Succeeded); Assert.Equal("Invalid email or password.", result.Error);
    }

    [Fact]
    public async Task Invalid_password_and_unknown_account_have_identical_safe_errors()
    {
        var user = User(UserRole.Customer, AccountStatus.Active);
        var invalid = await Service(new FakeStore(user)).LoginAsync(new(user.Email, "wrong"), Context(), default);
        var unknown = await Service(new FakeStore()).LoginAsync(new("unknown@example.com", "wrong"), Context(), default);
        Assert.Equal("Invalid email or password.", invalid.Error); Assert.Equal(invalid.Error, unknown.Error);
    }

    [Theory]
    [InlineData("0912345678")]
    [InlineData("+251912345678")]
    [InlineData("251912345678")]
    public async Task Ethiopian_phone_formats_resolve_to_the_same_account(string phone)
    {
        var user = User(UserRole.Customer, AccountStatus.Active); user.PhoneNumber = "+251912345678"; user.NormalizedPhoneNumber = "+251912345678";
        var result = await Service(new FakeStore(user)).LoginAsync(new(phone, "Correct1!"), Context(), default);
        Assert.True(result.Succeeded); Assert.Equal(user.Id, result.Value!.User.UserAccountId);
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Creator)]
    [InlineData(UserRole.MerchantAdmin)]
    [InlineData(UserRole.Cashier)]
    public async Task Normal_roles_enroll_and_unlock_pin_without_firebase(UserRole role)
    {
        var user=User(role,AccountStatus.Active); var service=Service(new FakeStore(user));
        Assert.True((await service.EnrollPinAsync(user.Id,new("12345","12345"),Context(),default)).Succeeded);
        var result=await service.UnlockWithPinAsync(new(user.PhoneNumber!,"12345"),Context(),default);
        Assert.True(result.Succeeded); Assert.Null(user.FirebaseUid); Assert.NotEqual("12345",user.PinHash);
    }

    [Fact]
    public async Task Tenth_incorrect_pin_persists_lock_and_recovery_resets_it()
    {
        var user=User(UserRole.Customer,AccountStatus.Active); user.PinHash="weymela-pin-v1:12345";
        var service=Service(new FakeStore(user));
        Result<TokenPair>? result=null;
        for(var i=0;i<10;i++){user.PinRetryNotBeforeUtc=null;result=await service.UnlockWithPinAsync(new(user.PhoneNumber!,"99999"),Context(),default);}
        Assert.Equal("Too many incorrect attempts. Use Forgot PIN to reset your PIN.",result!.Error); Assert.Equal(10,user.PinFailedAttemptCount); Assert.NotNull(user.PinLockedAtUtc);
        Assert.True((await service.ResetPinWithPasswordAsync(new(user.PhoneNumber!,"Correct1!","54321","54321"),Context(),default)).Succeeded);
        Assert.Equal(0,user.PinFailedAttemptCount); Assert.Null(user.PinLockedAtUtc);
        Assert.False((await service.UnlockWithPinAsync(new(user.PhoneNumber!,"12345"),Context(),default)).Succeeded);
        user.PinRetryNotBeforeUtc=null; Assert.True((await service.UnlockWithPinAsync(new(user.PhoneNumber!,"54321"),Context(),default)).Succeeded);
    }

    [Fact]
    public async Task Wrong_password_cannot_reset_pin()
    {
        var user=User(UserRole.Customer,AccountStatus.Active);
        var result=await Service(new FakeStore(user)).ResetPinWithPasswordAsync(new(user.PhoneNumber!,"wrong","54321","54321"),Context(),default);
        Assert.False(result.Succeeded);Assert.Equal("Invalid phone number or password.",result.Error);
    }

    [Fact]
    public async Task Platform_admin_pin_enrollment_is_rejected()
    {
        var user=User(UserRole.PlatformAdmin,AccountStatus.Active); var result=await Service(new FakeStore(user),new FakeFirebase(new("uid",user.Email,true))).LinkFirebaseAsync(user.Id,new("valid",user.Email),Context(),default);
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(false,"user@example.com")]
    [InlineData(true,"different@example.com")]
    public async Task Unverified_or_mismatched_firebase_identity_is_rejected(bool verified,string email)
    {
        var user=User(UserRole.Customer,AccountStatus.Active); var result=await Service(new FakeStore(user),new FakeFirebase(new("uid",email,verified))).LinkFirebaseAsync(user.Id,new("valid",user.Email),Context(),default);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Invalid_firebase_token_is_rejected()
    {
        var user=User(UserRole.Customer,AccountStatus.Active); var result=await Service(new FakeStore(user)).LinkFirebaseAsync(user.Id,new("invalid",user.Email),Context(),default);
        Assert.False(result.Succeeded); Assert.Equal("Invalid Firebase identity token.",result.Error);
    }

    [Fact]
    public async Task Duplicate_firebase_uid_linking_is_rejected_without_merging_accounts()
    {
        var first=User(UserRole.Customer,AccountStatus.Active); first.FirebaseUid="shared-uid"; first.RecoveryEmail=first.Email; first.NormalizedRecoveryEmail=first.NormalizedEmail; first.IsRecoveryEmailVerified=true;
        var second=User(UserRole.Creator,AccountStatus.Active); var store=new FakeStore(first,second);
        var result=await Service(store,new FakeFirebase(new("shared-uid",second.Email,true))).LinkFirebaseAsync(second.Id,new("valid",second.Email),Context(),default);
        Assert.False(result.Succeeded); Assert.Null(second.FirebaseUid);
    }

    private static readonly DateTime Now = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
    private static UserAccount User(UserRole role, AccountStatus status) => new() { Id = Guid.NewGuid(), Email = $"{role}@example.com", NormalizedEmail = $"{role}@example.com".ToUpperInvariant(), PhoneNumber = $"+2519{Math.Abs((int)role):D8}"[..13], NormalizedPhoneNumber = $"+2519{Math.Abs((int)role):D8}"[..13], PasswordHash = "Correct1!", Role = role, Status = status };
    private static RequestContext Context() => new("127.0.0.1", "tests", "correlation");
    private static AuthenticationService Service(FakeStore store,IFirebaseIdentityVerifier? firebase=null) => new(store, new FakePasswords(), new FakeTokens(), new FakeClock(), new FakeNotifier(), new PasswordPolicyValidator(Options.Create(new PasswordOptions())), Options.Create(new JwtOptions()), Options.Create(new LockoutOptions()), Options.Create(new PasswordResetOptions()), firebase??new FakeFirebase(null));

    private sealed class FakePasswords : IPasswordHasher { public string Hash(UserAccount user, string password) => password; public PasswordVerification Verify(UserAccount user, string hash, string password) => hash == password ? PasswordVerification.Success : PasswordVerification.Failed; }
    private sealed class FakeTokens : ITokenService { public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(UserAccount user) => ($"access-{user.Role}-{Guid.NewGuid():N}", Now.AddMinutes(15)); public string CreateOpaqueToken() => $"opaque-{Guid.NewGuid():N}"; public string HashToken(string token) => $"hash-{token}"; }
    private sealed class FakeClock : IUtcClock { public DateTime UtcNow => Now; }
    private sealed class FakeNotifier : IPasswordResetNotifier { public Task NotifyAsync(UserAccount user, string rawToken, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class FakeFirebase(FirebaseIdentityProof? proof) : IFirebaseIdentityVerifier { public Task<Result<FirebaseIdentityProof>> VerifyIdTokenAsync(string idToken,bool checkRevoked,CancellationToken ct)=>Task.FromResult(proof is null?Result<FirebaseIdentityProof>.Failure("Invalid Firebase identity token."):Result<FirebaseIdentityProof>.Success(proof)); }
    private sealed class FakeStore(params UserAccount[] users) : IAuthenticationStore
    {
        private readonly List<UserAccount> accounts = [.. users]; private readonly List<RefreshToken> refresh = []; private readonly List<PasswordResetToken> resets = []; private readonly List<PinResetAuthorization> pinResets=[];
        public Task<UserAccount?> FindUserByEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(accounts.SingleOrDefault(x => x.NormalizedEmail == normalizedEmail));
        public Task<UserAccount?> FindUserByPhoneAsync(string normalizedPhone, CancellationToken ct) => Task.FromResult(accounts.SingleOrDefault(x => x.NormalizedPhoneNumber == normalizedPhone));
        public Task<UserAccount?> FindUserAsync(Guid id, CancellationToken ct) => Task.FromResult(accounts.SingleOrDefault(x => x.Id == id));
        public Task<UserAccount?> FindUserByFirebaseUidAsync(string uid,CancellationToken ct)=>Task.FromResult(accounts.SingleOrDefault(x=>x.FirebaseUid==uid));
        public Task<UserAccount?> FindUserByRecoveryEmailAsync(string email,CancellationToken ct)=>Task.FromResult(accounts.SingleOrDefault(x=>x.NormalizedRecoveryEmail==email));
        public Task<RefreshToken?> FindRefreshAsync(string hash, CancellationToken ct) => Task.FromResult(refresh.SingleOrDefault(x => x.TokenHash == hash));
        public Task<PasswordResetToken?> FindResetAsync(string hash, CancellationToken ct) => Task.FromResult(resets.SingleOrDefault(x => x.TokenHash == hash));
        public Task<PinResetAuthorization?> FindPinResetAsync(string hash,CancellationToken ct)=>Task.FromResult(pinResets.SingleOrDefault(x=>x.TokenHash==hash));
        public void AddRefresh(RefreshToken token) => refresh.Add(token); public void AddReset(PasswordResetToken token) => resets.Add(token); public void AddPinReset(PinResetAuthorization token)=>pinResets.Add(token); public void AddAudit(LoginAudit audit) { }
        public Task RevokeFamilyAsync(string family, DateTime now, string reason, string? ip, CancellationToken ct) { foreach (var x in refresh.Where(x => x.TokenFamily == family)) { x.RevokedAtUtc = now; x.RevokedReason = reason; } return Task.CompletedTask; }
        public Task RevokeAllAsync(Guid userId, DateTime now, string reason, string? ip, CancellationToken ct) { foreach (var x in refresh.Where(x => x.UserAccountId == userId)) { x.RevokedAtUtc = now; x.RevokedReason = reason; } return Task.CompletedTask; }
        public Task<int> SaveAsync(CancellationToken ct) => Task.FromResult(1); public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }
}
