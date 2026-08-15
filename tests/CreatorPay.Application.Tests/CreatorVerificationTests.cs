using CreatorPay.Application.Authentication;
using CreatorPay.Application.Creators;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Tests;

public sealed class CreatorVerificationTests
{
    [Fact]
    public async Task Email_and_phone_tokens_verify_once_and_persist_pending_review_status()
    {
        var state = State(); var service = Service(state.Store);
        var email = await service.VerifyEmailAsync("email-token", default);
        Assert.True(email.Succeeded); Assert.True(state.User.IsEmailVerified); Assert.NotNull(state.Email.UsedAtUtc);
        var phone = await service.VerifyPhoneAsync("phone-token", default);
        Assert.True(phone.Succeeded); Assert.True(state.User.IsPhoneVerified); Assert.Equal(AccountStatus.PendingApproval, state.User.Status); Assert.Equal(CreatorStatus.PendingApproval, state.Creator.Status);
        var reused = await service.VerifyEmailAsync("email-token", default);
        Assert.False(reused.Succeeded); Assert.Equal("Invalid or expired verification token.", reused.Error);
    }

    [Fact]
    public async Task Wrong_and_expired_tokens_return_the_same_safe_error()
    {
        var state = State(); state.Email.ExpiresAtUtc = Now.AddSeconds(-1); var service = Service(state.Store);
        var wrong = await service.VerifyEmailAsync("wrong", default); var expired = await service.VerifyEmailAsync("email-token", default);
        Assert.Equal("Invalid or expired verification token.", wrong.Error); Assert.Equal(wrong.Error, expired.Error); Assert.False(state.User.IsEmailVerified);
    }

    private static readonly DateTime Now = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
    private static (FakeCreatorStore Store, UserAccount User, Creator Creator, CreatorVerificationToken Email, CreatorVerificationToken Phone) State()
    {
        var creator = new Creator { Id = Guid.NewGuid(), Status = CreatorStatus.PendingVerification };
        var user = new UserAccount { Id = Guid.NewGuid(), CreatorId = creator.Id, Role = UserRole.Creator, Status = AccountStatus.PendingVerification };
        var email = new CreatorVerificationToken { Id = Guid.NewGuid(), UserAccountId = user.Id, Purpose = "Email", TokenHash = "hash-email-token", ExpiresAtUtc = Now.AddMinutes(30) };
        var phone = new CreatorVerificationToken { Id = Guid.NewGuid(), UserAccountId = user.Id, Purpose = "Phone", TokenHash = "hash-phone-token", ExpiresAtUtc = Now.AddMinutes(30) };
        return (new(user, creator, email, phone), user, creator, email, phone);
    }
    private static CreatorService Service(FakeCreatorStore store) => new(store, new NoopPasswords(), new TestTokens(), new TestClock(), new NoopProvider(), new PasswordPolicyValidator(Options.Create(new PasswordOptions())), Options.Create(new CreatorVerificationOptions()), new NoopPhoneOtp());
    private sealed class TestClock : IUtcClock { public DateTime UtcNow => Now; }
    private sealed class TestTokens : ITokenService { public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(UserAccount user) => throw new NotSupportedException(); public string CreateOpaqueToken() => "unused"; public string HashToken(string token) => $"hash-{token}"; }
    private sealed class NoopPasswords : IPasswordHasher { public string Hash(UserAccount user, string password) => "hash"; public PasswordVerification Verify(UserAccount user, string hash, string password) => PasswordVerification.Failed; }
    private sealed class NoopProvider : ICreatorVerificationProvider { public Task SendEmailVerificationAsync(UserAccount user, string token, CancellationToken ct) => Task.CompletedTask; public Task SendPhoneVerificationAsync(Creator creator, string token, CancellationToken ct) => Task.CompletedTask; }
    private sealed class NoopPhoneOtp : IPhoneOtpService { public Task IssueAsync(UserAccount user, string purpose, string? ip, CancellationToken ct) => Task.CompletedTask; public Task<PhoneOtpResult> ResendAsync(string phone, string purpose, string? ip, CancellationToken ct) => Task.FromResult(PhoneOtpResult.Success()); public Task<PhoneOtpResult> VerifyRegistrationAsync(string phone, string code, CancellationToken ct) => Task.FromResult(PhoneOtpResult.Success()); public Task RequestPasswordResetAsync(string phone, string? ip, CancellationToken ct) => Task.CompletedTask; public Task<PhoneOtpResult> ResetPasswordAsync(ResetPasswordPhoneRequest request, string? ip, CancellationToken ct) => Task.FromResult(PhoneOtpResult.Success()); }
    private sealed class FakeCreatorStore(UserAccount user, Creator creator, params CreatorVerificationToken[] tokens) : ICreatorStore
    {
        public Task<CreatorVerificationToken?> FindTokenAsync(string hash, string purpose, CancellationToken ct) => Task.FromResult(tokens.SingleOrDefault(x => x.TokenHash == hash && x.Purpose == purpose));
        public Task<string> AllocateCreatorCodeAsync(CancellationToken ct) => Task.FromResult("4827");
        public Task<(UserAccount User, Creator Creator)?> FindByTokenAsync(string hash, string purpose, CancellationToken ct) => Task.FromResult(tokens.Any(x => x.TokenHash == hash && x.Purpose == purpose) ? ((UserAccount, Creator)?)(user, creator) : null);
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct); public Task<int> SaveAsync(CancellationToken ct) => Task.FromResult(1);
        public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId, CancellationToken ct) => Task.FromResult(false); public Task<bool> PhoneExistsAsync(string normalizedPhone, Guid? excludingCreatorId, CancellationToken ct) => Task.FromResult(false);
        public Task<UserAccount?> FindUserAsync(Guid id, CancellationToken ct) => Task.FromResult<UserAccount?>(user); public Task<UserAccount?> FindUserByCreatorAsync(Guid creatorId, CancellationToken ct) => Task.FromResult<UserAccount?>(user); public Task<Creator?> FindCreatorAsync(Guid id, CancellationToken ct) => Task.FromResult<Creator?>(creator);
        public Task<(UserAccount User, Creator Creator)?> FindByUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<(UserAccount, Creator)?>((user, creator)); public Task<IReadOnlyList<Creator>> FindByStatusAsync(CreatorStatus status, CancellationToken ct) => Task.FromResult<IReadOnlyList<Creator>>([]);
        public Task InvalidateTokensAsync(Guid userId, string purpose, DateTime usedAtUtc, CancellationToken ct) => Task.CompletedTask; public void Add(UserAccount item) { }
        public void Add(Creator item) { }
        public void Add(CreatorVerificationToken item) { }
        public void Add(CreatorAuditEvent item) { }
        public void Add(CreatorSocialProfile item) { }
        public void RemoveSocialProfiles(IEnumerable<CreatorSocialProfile> profiles) { }
    }
}
