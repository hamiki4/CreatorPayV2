using CreatorPay.Application.Authentication;
using CreatorPay.Application.Creators;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CreatorPay.Application.Tests;

public sealed class CreatorRegistrationValidationTests
{
    [Theory]
    [InlineData(50_000, 60_000, true)]
    [InlineData(50_000, 40_000, false)]
    public async Task TikTok_signup_enforces_admin_minimum(long minimum, long followers, bool expectedSuccess)
    {
        var service = Service(new FakeCreatorStore(minimum));
        var result = await service.RegisterAsync(Request("TikTok", followers), default);
        Assert.Equal(expectedSuccess, result.Succeeded);
        if (!expectedSuccess)
        {
            Assert.Equal($"Minimum required TikTok followers: {minimum:N0}.", result.Error);
        }
    }

    [Fact]
    public async Task Non_TikTok_signup_is_not_subject_to_the_minimum()
    {
        var service = Service(new FakeCreatorStore(60_000));
        var result = await service.RegisterAsync(Request("Instagram", 1_000), default);
        Assert.True(result.Succeeded, result.Error);
    }

    private static RegisterCreatorRequest Request(string platform, long followers) => new(
        FirstName: "Abebe",
        LastName: "Kebede",
        DisplayName: "Abebe",
        PhoneNumber: "0911000022",
        Email: "creator01@example.com",
        Password: "Welcome1!",
        PreferredLanguage: "en",
        City: "Addis Ababa",
        Biography: "",
        ContentCategories: "",
        TermsAccepted: true,
        SocialProfiles: [new SocialProfileRequest((SocialPlatform)Enum.Parse(typeof(SocialPlatform), platform), "handle", $"https://example.com/{platform.ToLowerInvariant()}", followers, true)],
        Confirmation: "Welcome1!");

    private static CreatorService Service(FakeCreatorStore store) => new(store, new NoopPasswords(), new TestTokens(), new TestClock(), new NoopProvider(), new PasswordPolicyValidator(Options.Create(new PasswordOptions())), Options.Create(new CreatorVerificationOptions()), new NoopPhoneOtp(), new NoopPhotoStore());
    private sealed class TestClock : IUtcClock { public DateTime UtcNow => new(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc); }
    private sealed class TestTokens : ITokenService { public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(UserAccount user) => throw new NotSupportedException(); public string CreateOpaqueToken() => "unused"; public string HashToken(string token) => $"hash-{token}"; }
    private sealed class NoopPasswords : IPasswordHasher { public string Hash(UserAccount user, string password) => "hash"; public PasswordVerification Verify(UserAccount user, string hash, string password) => PasswordVerification.Failed; }
    private sealed class NoopProvider : ICreatorVerificationProvider { public Task SendEmailVerificationAsync(UserAccount user, string token, CancellationToken ct) => Task.CompletedTask; public Task SendPhoneVerificationAsync(Creator creator, string token, CancellationToken ct) => Task.CompletedTask; }
    private sealed class NoopPhoneOtp : IPhoneOtpService { public Task IssueAsync(UserAccount user, string purpose, string? ip, CancellationToken ct) => Task.CompletedTask; public Task<PhoneOtpResult> ResendAsync(string phone, string purpose, string? ip, CancellationToken ct) => Task.FromResult(PhoneOtpResult.Success()); public Task<PhoneOtpResult> VerifyRegistrationAsync(string phone, string code, CancellationToken ct) => Task.FromResult(PhoneOtpResult.Success()); public Task RequestPasswordResetAsync(string phone, string? ip, CancellationToken ct) => Task.CompletedTask; public Task<PhoneOtpResult> ResetPasswordAsync(ResetPasswordPhoneRequest request, string? ip, CancellationToken ct) => Task.FromResult(PhoneOtpResult.Success()); }
    private sealed class NoopPhotoStore : ICreatorProfilePhotoStore { public Task DeleteAsync(string storageKey, CancellationToken ct) => Task.CompletedTask; public Task<Stream> OpenAsync(string storageKey, CancellationToken ct) => Task.FromResult<Stream>(new MemoryStream()); public Task<(string StorageKey, long SizeBytes)> SaveAsync(Guid creatorId, Stream content, string contentType, long sizeBytes, CancellationToken ct) => Task.FromResult((StorageKey: $"creator/{creatorId:N}.png", SizeBytes: sizeBytes)); }
    private sealed class FakeCreatorStore(long minimumTikTokFollowers) : ICreatorStore
    {
        public Task<long> GetMinimumTikTokFollowersAsync(CancellationToken ct) => Task.FromResult(minimumTikTokFollowers);
        public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> PhoneExistsAsync(string normalizedPhone, Guid? excludingCreatorId, CancellationToken ct) => Task.FromResult(false);
        public Task<string> AllocateCreatorCodeAsync(CancellationToken ct) => Task.FromResult("4827");
        public Task<UserAccount?> FindUserAsync(Guid id, CancellationToken ct) => Task.FromResult<UserAccount?>(null);
        public Task<UserAccount?> FindUserByCreatorAsync(Guid creatorId, CancellationToken ct) => Task.FromResult<UserAccount?>(null);
        public Task<Creator?> FindCreatorAsync(Guid id, CancellationToken ct) => Task.FromResult<Creator?>(null);
        public Task<(UserAccount User, Creator Creator)?> FindByUserAsync(Guid userId, CancellationToken ct) => Task.FromResult<(UserAccount, Creator)?>(null);
        public Task<(UserAccount User, Creator Creator)?> FindByTokenAsync(string hash, string purpose, CancellationToken ct) => Task.FromResult<(UserAccount, Creator)?>(null);
        public Task<CreatorVerificationToken?> FindTokenAsync(string hash, string purpose, CancellationToken ct) => Task.FromResult<CreatorVerificationToken?>(null);
        public Task<IReadOnlyList<Creator>> FindByStatusAsync(CreatorStatus status, CancellationToken ct) => Task.FromResult<IReadOnlyList<Creator>>([]);
        public Task InvalidateTokensAsync(Guid userId, string purpose, DateTime usedAtUtc, CancellationToken ct) => Task.CompletedTask;
        public void Add(UserAccount item) { }
        public void Add(Creator item) { }
        public void Add(CreatorVerificationToken item) { }
        public void Add(CreatorAuditEvent item) { }
        public void Add(CreatorSocialProfile item) { }
        public void RemoveSocialProfiles(IEnumerable<CreatorSocialProfile> profiles) { }
        public Task<int> SaveAsync(CancellationToken ct) => Task.FromResult(1);
        public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) => action(ct);
    }
}
