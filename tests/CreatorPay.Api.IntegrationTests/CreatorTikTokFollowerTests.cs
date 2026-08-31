using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class CreatorTikTokFollowerTests : IAsyncLifetime
{
    private const string SigningKey = "development-only-replace-this-signing-key-000000";
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("creatorpay_tiktok_minimum_tests").WithUsername("creatorpay").WithPassword("test-only-password").Build();
    private WebApplicationFactory<Program>? factory;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("Authentication:Jwt:Issuer", "CreatorPay");
            builder.UseSetting("Authentication:Jwt:Audience", "CreatorPay.Web");
            builder.UseSetting("Authentication:Jwt:SigningKey", SigningKey);
            builder.UseSetting("ConnectionStrings:CreatorPayDatabase", container.GetConnectionString());
            builder.UseSetting("SmsOtp:SmsProvider", "PilotTest");
            builder.UseSetting("SmsOtp:HashSecret", "test-only-phone-otp-secret-000000000000000");
            builder.UseSetting("SmsOtp:TestCode", "654321");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CreatorPayDatabase"] = container.GetConnectionString(),
                ["Authentication:Jwt:Issuer"] = "CreatorPay",
                ["Authentication:Jwt:Audience"] = "CreatorPay.Web",
                ["Authentication:Jwt:SigningKey"] = SigningKey,
                ["CustomerVerification:HmacSecret"] = "test-only-hmac-secret-0000000000000000000000",
                ["CustomerVerification:EncryptionKey"] = "test-only-encryption-key-000000000000000000",
                ["SmsOtp:SmsProvider"] = "PilotTest",
                ["SmsOtp:HashSecret"] = "test-only-phone-otp-secret-000000000000000",
                ["SmsOtp:TestCode"] = "654321",
                ["CreatorPayouts:CreatorEarningHoldingPeriodDays"] = "0",
                ["CreatorPayouts:MinimumCreatorPayoutAmount"] = "0",
                ["Checkout:CashbackPayoutThreshold"] = "0",
                ["DepositProofStorage:RootPath"] = Path.Combine(Path.GetTempPath(), $"creatorpay-tiktok-tests-{Guid.NewGuid():N}"),
                ["Cors:AllowedOrigins:0"] = "http://127.0.0.1"
            }));
        });
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        await container.DisposeAsync();
    }

    [DockerFact]
    public async Task TikTok_signup_obeys_dynamic_admin_minimum_and_public_settings_endpoint_reflects_it()
    {
        await ResetFinancialSettingsAsync();
        using var client = factory!.CreateClient();
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");

        await SetMinimumTikTokFollowersAsync(50_000L);

        var signupSettings = await Get(client, "/api/v1/auth/signup-settings");
        Assert.Equal(HttpStatusCode.OK, signupSettings.StatusCode);
        var signupSettingsBody = await signupSettings.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50_000L, signupSettingsBody!.GetProperty("minimumTikTokFollowers").GetInt64());

        const string password = "Welcome1!";
        var allowed = await client.PostAsJsonAsync("/api/v1/creators/register", CreatorRequest("TikTok", 60_000, password, "above-floor@example.com", "Above Floor"));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        _ = await allowed.Content.ReadFromJsonAsync<JsonElement>();

        var blocked = await client.PostAsJsonAsync("/api/v1/creators/register", CreatorRequest("TikTok", 40_000, password, "below-floor@example.com", "Below Floor"));
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("Minimum required TikTok followers: 50,000.", await blocked.Content.ReadAsStringAsync());

        await SetMinimumTikTokFollowersAsync(60_000L);

        signupSettings = await Get(client, "/api/v1/auth/signup-settings");
        Assert.Equal(60_000L, (await signupSettings.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("minimumTikTokFollowers").GetInt64());

        var blockedAfterRaise = await client.PostAsJsonAsync("/api/v1/creators/register", CreatorRequest("TikTok", 55_000, password, "below-new-floor@example.com", "Below New Floor"));
        Assert.Equal(HttpStatusCode.BadRequest, blockedAfterRaise.StatusCode);
        Assert.Contains("Minimum required TikTok followers: 60,000.", await blockedAfterRaise.Content.ReadAsStringAsync());

        await using var db = Db();
        Assert.True(await db.UserAccounts.AnyAsync(x => x.NormalizedEmail == "ABOVE-FLOOR@EXAMPLE.COM"));
        Assert.True(await db.Creators.AnyAsync(x => x.DisplayName == "Above Floor"));
        Assert.Equal(1, await db.Creators.CountAsync(x => x.DisplayName == "Above Floor" || x.DisplayName == "Below Floor"));

        var pending = await Get(client, "/api/v1/creators/pending", admin);
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        var pendingBody = await pending.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(pendingBody!.EnumerateArray(), x => x.GetProperty("displayName").GetString() == "Above Floor");
    }

    [DockerFact]
    public async Task NonTikTok_signup_is_not_gated_by_the_minimum_and_creator_review_still_works()
    {
        await ResetFinancialSettingsAsync();
        using var client = factory!.CreateClient();
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");

        await SetMinimumTikTokFollowersAsync(60_000L);

        var instagram = await client.PostAsJsonAsync("/api/v1/creators/register", CreatorRequest("Instagram", 1_000, "Welcome1!", "instagram-creator@example.com", "Instagram Creator"));
        Assert.Equal(HttpStatusCode.Created, instagram.StatusCode);

        var pending = await Get(client, "/api/v1/creators/pending", admin);
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        var body = await pending.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(body!.EnumerateArray(), x => x.GetProperty("displayName").GetString() == "Instagram Creator");
    }

    [DockerFact]
    public async Task Creator_signup_rejects_a_phone_owned_by_another_account_role_before_persistence()
    {
        await ResetFinancialSettingsAsync();
        await SetMinimumTikTokFollowersAsync(50_000L);

        await using (var db = Db())
        {
            db.UserAccounts.Add(new UserAccount
            {
                Id = Guid.NewGuid(),
                DisplayName = "Existing Operations Admin",
                Email = "existing-operations@example.com",
                NormalizedEmail = "EXISTING-OPERATIONS@EXAMPLE.COM",
                PhoneNumber = "+251911000888",
                NormalizedPhoneNumber = "+251911000888",
                PasswordHash = "test-only",
                Role = UserRole.OperationsAdmin,
                Status = AccountStatus.Active,
                IsEmailVerified = true,
                IsPhoneVerified = true,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = factory!.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/creators/register",
            CreatorRequest("TikTok", 60_000, "Welcome1!", "phone-collision@example.com", "Phone Collision", "0911000888"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Phone number is already registered.", await response.Content.ReadAsStringAsync());

        await using var verification = Db();
        Assert.False(await verification.UserAccounts.AnyAsync(x => x.NormalizedEmail == "PHONE-COLLISION@EXAMPLE.COM"));
        Assert.False(await verification.Creators.AnyAsync(x => x.DisplayName == "Phone Collision"));
        Assert.False(await verification.CreatorSocialProfiles.AnyAsync(x => x.Handle == "phonecollision"));
    }

    private static object CreatorRequest(string platform, long followers, string password, string email, string displayName, string phoneNumber = "0911000777") => new
    {
        firstName = "Test",
        lastName = "Creator",
        displayName,
        phoneNumber,
        email,
        password,
        confirmation = password,
        preferredLanguage = "en",
        city = "Addis Ababa",
        biography = "",
        contentCategories = "Lifestyle",
        termsAccepted = true,
        socialProfiles = new[] { new { platform, handle = $"{displayName.Replace(" ", string.Empty).ToLowerInvariant()}", profileUrl = $"https://www.{platform.ToLowerInvariant()}.com/@{displayName.Replace(" ", string.Empty).ToLowerInvariant()}", followerCount = followers, isPrimary = true } }
    };

    private static Task<HttpResponseMessage> Get(HttpClient client, string path)
    {
        return client.GetAsync(path);
    }

    private static Task<HttpResponseMessage> Get(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, string path, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private async Task ResetFinancialSettingsAsync()
    {
        await using var db = Db();
        await db.PlatformCommissionAssignments.ExecuteDeleteAsync();
        await db.CommissionRuleVersions.ExecuteDeleteAsync();
        await db.CommissionRules.ExecuteDeleteAsync();
        await db.CommissionPlans.ExecuteDeleteAsync();
        await db.PayoutScheduleVersions.ExecuteDeleteAsync();
        await db.PlatformFinancialSettings.ExecuteDeleteAsync();
        await db.SaveChangesAsync();
    }

    private async Task SetMinimumTikTokFollowersAsync(long minimumTikTokFollowers)
    {
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();
        var setting = await db.PlatformFinancialSettings.SingleOrDefaultAsync(x => x.CurrencyCode == "ETB");
        setting ??= new PlatformFinancialSetting
        {
            Id = Guid.NewGuid(),
            CurrencyCode = "ETB",
            CreatedAtUtc = DateTime.UtcNow,
            MinimumBusinessWalletBalance = 0m
        };
        setting.MinimumTikTokFollowers = minimumTikTokFollowers;
        setting.ChangedByUserId = Guid.Parse("90000000-0000-0000-0000-000000000001");
        setting.ChangedAtUtc = DateTime.UtcNow;
        setting.UpdatedAtUtc = DateTime.UtcNow;
        setting.UpdatedBy = "90000000-0000-0000-0000-000000000001";
        if (db.Entry(setting).State == EntityState.Detached) db.Add(setting);
        var otherVersion = await db.BusinessTypeWalletMinimumVersions.SingleOrDefaultAsync(x => x.CurrencyCode == "ETB" && x.BusinessType == "Other");
        otherVersion ??= new BusinessTypeWalletMinimumVersion
        {
            Id = Guid.NewGuid(),
            CurrencyCode = "ETB",
            BusinessType = "Other",
            VersionNumber = 1,
            MinimumBusinessWalletBalance = 0m,
            EffectiveFromUtc = DateTime.UtcNow,
            ChangedByUserId = Guid.Parse("90000000-0000-0000-0000-000000000001"),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "90000000-0000-0000-0000-000000000001"
        };
        otherVersion.MinimumBusinessWalletBalance = 0m;
        otherVersion.EffectiveFromUtc = DateTime.UtcNow;
        if (db.Entry(otherVersion).State == EntityState.Detached) db.BusinessTypeWalletMinimumVersions.Add(otherVersion);
        await db.SaveChangesAsync();
    }

    private static string Token(UserRole role, string userId, AccountStatus status = AccountStatus.Active)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role.ToString()),
            new(AuthenticationClaimTypes.AccountStatus, status.ToString()),
            new("test_token", "true")
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: credentials));
    }

    private ApplicationDbContext Db() => factory!.Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
}
