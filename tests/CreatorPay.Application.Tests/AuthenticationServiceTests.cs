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
    [InlineData(UserRole.Customer, AccountStatus.PendingVerification)]
    [InlineData(UserRole.Creator, AccountStatus.PendingVerification)]
    [InlineData(UserRole.Creator, AccountStatus.PendingApproval)]
    [InlineData(UserRole.MerchantAdmin, AccountStatus.PendingVerification)]
    [InlineData(UserRole.MerchantAdmin, AccountStatus.PendingApproval)]
    public async Task Public_pending_account_can_sign_in_for_onboarding(UserRole role, AccountStatus status)
    {
        var user = User(role, status); var result = await Service(new FakeStore(user)).LoginAsync(new(user.Email, "Correct1!"), Context(), default);
        Assert.True(result.Succeeded); Assert.Equal(status, result.Value!.User.Status);
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

    private static readonly DateTime Now = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
    private static UserAccount User(UserRole role, AccountStatus status) => new() { Id = Guid.NewGuid(), Email = $"{role}@example.com", NormalizedEmail = $"{role}@example.com".ToUpperInvariant(), PasswordHash = "Correct1!", Role = role, Status = status };
    private static RequestContext Context() => new("127.0.0.1", "tests", "correlation");
    private static AuthenticationService Service(FakeStore store) => new(store, new FakePasswords(), new FakeTokens(), new FakeClock(), new FakeNotifier(), new PasswordPolicyValidator(Options.Create(new PasswordOptions())), Options.Create(new JwtOptions()), Options.Create(new LockoutOptions()), Options.Create(new PasswordResetOptions()));

    private sealed class FakePasswords : IPasswordHasher { public string Hash(UserAccount user, string password) => password; public PasswordVerification Verify(UserAccount user, string hash, string password) => hash == password ? PasswordVerification.Success : PasswordVerification.Failed; }
    private sealed class FakeTokens : ITokenService { public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(UserAccount user) => ($"access-{user.Role}-{Guid.NewGuid():N}", Now.AddMinutes(15)); public string CreateOpaqueToken() => $"opaque-{Guid.NewGuid():N}"; public string HashToken(string token) => $"hash-{token}"; }
    private sealed class FakeClock : IUtcClock { public DateTime UtcNow => Now; }
    private sealed class FakeNotifier : IPasswordResetNotifier { public Task NotifyAsync(UserAccount user, string rawToken, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class FakeStore(params UserAccount[] users) : IAuthenticationStore
    {
        private readonly List<UserAccount> accounts = [.. users]; private readonly List<RefreshToken> refresh = []; private readonly List<PasswordResetToken> resets = [];
        public Task<UserAccount?> FindUserByEmailAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult(accounts.SingleOrDefault(x => x.NormalizedEmail == normalizedEmail));
        public Task<UserAccount?> FindUserAsync(Guid id, CancellationToken ct) => Task.FromResult(accounts.SingleOrDefault(x => x.Id == id));
        public Task<RefreshToken?> FindRefreshAsync(string hash, CancellationToken ct) => Task.FromResult(refresh.SingleOrDefault(x => x.TokenHash == hash));
        public Task<PasswordResetToken?> FindResetAsync(string hash, CancellationToken ct) => Task.FromResult(resets.SingleOrDefault(x => x.TokenHash == hash));
        public void AddRefresh(RefreshToken token) => refresh.Add(token); public void AddReset(PasswordResetToken token) => resets.Add(token); public void AddAudit(LoginAudit audit) { }
        public Task RevokeFamilyAsync(string family, DateTime now, string reason, string? ip, CancellationToken ct) { foreach (var x in refresh.Where(x => x.TokenFamily == family)) { x.RevokedAtUtc = now; x.RevokedReason = reason; } return Task.CompletedTask; }
        public Task RevokeAllAsync(Guid userId, DateTime now, string reason, string? ip, CancellationToken ct) { foreach (var x in refresh.Where(x => x.UserAccountId == userId)) { x.RevokedAtUtc = now; x.RevokedReason = reason; } return Task.CompletedTask; }
        public Task<int> SaveAsync(CancellationToken ct) => Task.FromResult(1); public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }
}
