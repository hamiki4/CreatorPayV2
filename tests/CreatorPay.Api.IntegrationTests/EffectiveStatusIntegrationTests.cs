using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CreatorPay.Application.Accounts;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Eligibility;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class EffectiveStatusIntegrationTests : IAsyncLifetime
{
    private const string SigningKey = "effective-status-test-signing-key-000000";
    private const string SeedPassword = "E2e-test-password-1!";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("creatorpay_effective_status").WithUsername("creatorpay").WithPassword("test-only-password").Build();
    private WebApplicationFactory<Program>? factory;
    private sealed record LoginResponse(string AccessToken);

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:CreatorPayDatabase"] = database.GetConnectionString(),
            ["Authentication:Jwt:Issuer"] = "CreatorPay",
            ["Authentication:Jwt:Audience"] = "CreatorPay.Web",
            ["Authentication:Jwt:SigningKey"] = SigningKey,
            ["CustomerVerification:HmacSecret"] = new string('h', 32),
            ["CustomerVerification:EncryptionKey"] = new string('e', 32),
            ["SmsOtp:SmsProvider"] = "PilotTest",
            ["SmsOtp:HashSecret"] = new string('o', 32),
            ["SmsOtp:TestCode"] = "654321",
            ["Support:Email"] = "tests@example.invalid",
            ["Storage:Provider"] = "MetadataOnly"
        };
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            foreach (var value in values) builder.UseSetting(value.Key, value.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureLogging(logging => logging.ClearProviders());
        });
        await using var db = Db();
        await db.Database.MigrateAsync();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = SeedPassword })).StatusCode);
    }

    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    [Fact]
    public async Task Creator_profile_and_lock_cycle_use_effective_status()
    {
        var admin = await AuthClientAsync("admin@e2e.invalid", SeedPassword);
        var creatorId = Guid.Parse("33000000-0000-0000-0000-000000000001");
        var creatorUserId = Guid.Parse("33000000-0000-0000-0000-000000000011");

        var pendingCreator = await AuthClientAsync("creator-pending-1@e2e.invalid", SeedPassword);
        var pendingProfile = await Get(pendingCreator, "/api/v1/creators/me");
        Assert.Equal(HttpStatusCode.OK, pendingProfile.StatusCode);
        Assert.Equal("Inactive", (await pendingProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("effectiveStatus").GetString());

        Assert.Equal(HttpStatusCode.OK, (await Post(admin, "/api/v1/creators/approve", new { creatorId, reason = (string?)null })).StatusCode);

        var approvedCreator = await AuthClientAsync("creator-pending-1@e2e.invalid", SeedPassword);
        var approvedProfile = await Get(approvedCreator, "/api/v1/creators/me");
        var approvedBody = await approvedProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Active", approvedBody!.GetProperty("effectiveStatus").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/v1/admin/accounts/{creatorUserId}/lock", new { reason = "Lock for testing" })).StatusCode);
        var lockedStatus = await CreatorEffectiveStatusAsync(creatorId);
        Assert.Equal("Inactive", lockedStatus.EffectiveStatus);
        Assert.Equal("Locked", lockedStatus.EffectiveStatusReason);

        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/v1/admin/accounts/{creatorUserId}/unlock", new { reason = "Unlock for testing" })).StatusCode);
        var unlockedProfile = await Get(approvedCreator, "/api/v1/creators/me");
        Assert.Equal("Active", (await unlockedProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("effectiveStatus").GetString());
    }

    [Fact]
    public async Task Business_profile_recalculates_for_approval_funding_lock_and_business_type_changes()
    {
        var admin = await AuthClientAsync("admin@e2e.invalid", SeedPassword);
        var register = await RegisterMerchantAsync();
        var merchantId = register.merchantId;
        var merchantEmail = register.email;
        var merchantPassword = register.password;
        var ownerAccountId = await MerchantUserAccountIdAsync(merchantId);

        var pendingMerchant = await AuthClientAsync(merchantEmail, merchantPassword);
        var pendingProfile = await Get(pendingMerchant, "/api/v1/merchants/me");
        Assert.Equal("Inactive", (await pendingProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("effectiveStatus").GetString());

        Assert.Equal(HttpStatusCode.OK, (await Post(admin, "/api/v1/admin/merchants/approve", new { merchantId, reason = (string?)null })).StatusCode);
        await EnsureWalletAsync(merchantId, 1000m);

        await SetBusinessTypeMinimumAsync("Restaurant / Café", 1200m);
        var approvedMerchant = await AuthClientAsync(merchantEmail, merchantPassword);
        var underfundedProfile = await Get(approvedMerchant, "/api/v1/merchants/me");
        var underfundedBody = await underfundedProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Inactive", underfundedBody!.GetProperty("effectiveStatus").GetString());
        Assert.Equal("Underfunded", underfundedBody.GetProperty("effectiveStatusReason").GetString());

        await SetWalletBalanceAsync(merchantId, 1200m);
        var fundedProfile = await Get(approvedMerchant, "/api/v1/merchants/me");
        Assert.Equal("Active", (await fundedProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("effectiveStatus").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/v1/admin/accounts/{ownerAccountId}/lock", new { reason = "Lock for testing" })).StatusCode);
        var lockedStatus = await MerchantEffectiveStatusAsync(merchantId);
        Assert.Equal("Inactive", lockedStatus.EffectiveStatus);
        Assert.Equal("Locked", lockedStatus.EffectiveStatusReason);

        Assert.Equal(HttpStatusCode.NoContent, (await Post(admin, $"/api/v1/admin/accounts/{ownerAccountId}/unlock", new { reason = "Unlock for testing" })).StatusCode);
        var unlockedProfile = await Get(approvedMerchant, "/api/v1/merchants/me");
        Assert.Equal("Active", (await unlockedProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("effectiveStatus").GetString());

        Assert.Equal(HttpStatusCode.OK, (await Put(admin, $"/api/v1/admin/merchants/{merchantId}/business-type", new { businessType = "Electronics" })).StatusCode);
        await SetBusinessTypeMinimumAsync("Electronics", 5000m);
        await SetWalletBalanceAsync(merchantId, 2000m);
        var thresholdProfile = await Get(approvedMerchant, "/api/v1/merchants/me");
        Assert.Equal("Inactive", (await thresholdProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("effectiveStatus").GetString());
        await SetWalletBalanceAsync(merchantId, 5000m);
        var restoredProfile = await Get(approvedMerchant, "/api/v1/merchants/me");
        Assert.Equal("Active", (await restoredProfile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("effectiveStatus").GetString());
    }

    [Fact]
    public async Task Underfunded_locked_and_restored_businesses_move_in_and_out_of_customer_discovery()
    {
        var adminClient = AuthedClient(Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001"));
        var ownerClient = AuthedClient(Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001"));
        var creatorClient = AuthedClient(Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001"));
        var shopperClient = AuthedClient(Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001"));
        var merchantId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var ownerAccountId = Guid.Parse("20000000-0000-0000-0000-000000000008");
        var relationshipId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        const string videoUrl = "https://www.tiktok.com/@active-e2e-business/video/1234567890123456789";

        await using (var db = Db())
        {
            var relationship = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == relationshipId);
            var campaignIds = await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == relationshipId).Select(x => x.Id).ToArrayAsync();
            relationship.AssignedCampaignId = null;
            await db.SaveChangesAsync();
            await db.CheckoutSessions.Where(x => campaignIds.Contains(x.CampaignId)).ExecuteDeleteAsync();
            await db.SavedPromotions.Where(x => campaignIds.Contains(x.CampaignId)).ExecuteDeleteAsync();
            await db.CampaignCommissionAssignments.Where(x => x.MerchantCreatorPartnershipId == relationshipId).ExecuteDeleteAsync();
            await db.CampaignQrCodes.Where(x => campaignIds.Contains(x.CampaignId)).ExecuteDeleteAsync();
            await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == relationshipId).ExecuteDeleteAsync();
        }

        await SetBusinessTypeMinimumAsync("Other", 1000m);
        await SetWalletBalanceAsync(merchantId, 1000m);
        Assert.Equal(HttpStatusCode.OK, (await Post(ownerClient, $"/api/v1/merchant/partnerships/{relationshipId}/activate", new { reason = "Activate disposable promotion" })).StatusCode);
        var promoVideo = await Post(creatorClient, $"/api/v1/creator/partnerships/{relationshipId}/promotion-video", new { videoUrl });
        Assert.Equal(HttpStatusCode.Created, promoVideo.StatusCode);
        var promoVideoId = (await promoVideo.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())!.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(ownerClient, $"/api/v1/merchant/promotion-videos/{promoVideoId}/approve", new { reason = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(creatorClient, $"/api/v1/creator/partnerships/{relationshipId}/go-live", new { })).StatusCode);

        var funded = await Get(shopperClient, "/api/v1/customer/discovery/advertising?q=Active");
        var fundedRows = await funded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(fundedRows!.EnumerateArray(), x => x.GetProperty("relationshipId").GetGuid() == relationshipId);

        await SetWalletBalanceAsync(merchantId, 0m);
        var underfunded = await Get(shopperClient, "/api/v1/customer/discovery/advertising?q=Active");
        var underfundedRows = await underfunded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(underfundedRows!.EnumerateArray(), x => x.GetProperty("relationshipId").GetGuid() == relationshipId);

        await SetWalletBalanceAsync(merchantId, 1000m);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(adminClient, $"/api/v1/admin/accounts/{ownerAccountId}/lock", new { reason = "Lock for discovery test" })).StatusCode);
        var locked = await Get(shopperClient, "/api/v1/customer/discovery/advertising?q=Active");
        var lockedRows = await locked.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(lockedRows!.EnumerateArray(), x => x.GetProperty("relationshipId").GetGuid() == relationshipId);

        Assert.Equal(HttpStatusCode.NoContent, (await Post(adminClient, $"/api/v1/admin/accounts/{ownerAccountId}/unlock", new { reason = "Unlock for discovery test" })).StatusCode);
        var restored = await Get(shopperClient, "/api/v1/customer/discovery/advertising?q=Active");
        var restoredRows = await restored.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(restoredRows!.EnumerateArray(), x => x.GetProperty("relationshipId").GetGuid() == relationshipId);
    }

    private async Task<(Guid merchantId, string email, string password)> RegisterMerchantAsync()
    {
        var email = $"pilot-business-{Guid.NewGuid():N}@e2e.invalid";
        var password = "Pilot-business-1!";
        using var client = factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/merchants/register", new
        {
            legalBusinessName = "Pilot Business PLC",
            tradingName = "Pilot Business",
            businessType = "Restaurant / Café",
            taxRegistrationNumber = (string?)null,
            phoneNumber = "+251911234567",
            email,
            password,
            businessAddress = "Piazza Building",
            city = "Addis Ababa",
            region = "Addis Ababa",
            country = "Ethiopia",
            timeZone = "Africa/Addis_Ababa",
            documents = (object?)null,
            primaryContactName = "Pilot Owner",
            businessRegistrationNumber = (string?)null,
            preferredLanguage = "en",
            termsAccepted = true,
            confirmation = password
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return (body!.GetProperty("merchantId").GetGuid(), email, password);
    }

    private async Task<string> LoginTokenAsync(string email, string password)
    {
        using var client = factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }

    private async Task<HttpClient> AuthClientAsync(string email, string password)
    {
        var token = await LoginTokenAsync(email, password);
        var client = factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HttpClient AuthedClient(string token)
    {
        var client = factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string Token(UserRole role, string userId, string? customerId = null, string? merchantId = null, string? cashierId = null, string? creatorId = null, AccountStatus status = AccountStatus.Active)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId), new(ClaimTypes.Role, role.ToString()), new(AuthenticationClaimTypes.AccountStatus, status.ToString()), new("test_token", "true") };
        if (customerId is not null) claims.Add(new("customer_id", customerId));
        if (merchantId is not null) claims.Add(new("merchant_id", merchantId));
        if (cashierId is not null) claims.Add(new("cashier_id", cashierId));
        if (creatorId is not null) claims.Add(new("creator_id", creatorId));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: credentials));
    }

    private async Task<Guid> MerchantUserAccountIdAsync(Guid merchantId)
    {
        await using var db = Db();
        return await db.UserAccounts.Where(x => x.MerchantId == merchantId && x.Role == UserRole.MerchantAdmin).Select(x => x.Id).SingleAsync();
    }

    private async Task<EffectiveAccountStatusDto> CreatorEffectiveStatusAsync(Guid creatorId)
    {
        await using var db = Db();
        var pair = await db.UserAccounts.Where(x => x.CreatorId == creatorId).SingleAsync();
        var creator = await db.Creators.SingleAsync(x => x.Id == creatorId);
        return EffectiveAccountStatus.FromCreator(pair, creator, DateTime.UtcNow);
    }

    private async Task<EffectiveAccountStatusDto> MerchantEffectiveStatusAsync(Guid merchantId)
    {
        await using var db = Db();
        var user = await db.UserAccounts.SingleAsync(x => x.MerchantId == merchantId);
        var merchant = await db.Merchants.SingleAsync(x => x.Id == merchantId);
        var walletBalance = await db.MerchantWallets.Where(x => x.MerchantId == merchantId && x.CurrencyCode == "ETB").Select(x => x.AvailableBalance).SingleAsync();
        var minimum = await BusinessWalletMinimumQueries.CurrentMinimumAsync(db, "ETB", merchantId, CancellationToken.None);
        var wallet = new CreatorPay.Application.Wallet.WalletDto(Guid.NewGuid(), "ETB", walletBalance, 0m, "Active", minimum);
        return EffectiveAccountStatus.FromMerchant(user, merchant, wallet, DateTime.UtcNow);
    }

    private async Task EnsureWalletAsync(Guid merchantId, decimal minimumBalance)
    {
        await using var db = Db();
        var wallet = await db.MerchantWallets.SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.CurrencyCode == "ETB");
        if (wallet is null)
        {
            wallet = new MerchantWallet { Id = Guid.NewGuid(), MerchantId = merchantId, CurrencyCode = "ETB", CreatedAtUtc = DateTime.UtcNow };
            db.MerchantWallets.Add(wallet);
        }
        db.Entry(wallet).Property(nameof(MerchantWallet.AvailableBalance)).CurrentValue = minimumBalance;
        await db.SaveChangesAsync();
    }

    private async Task SetWalletBalanceAsync(Guid merchantId, decimal balance)
    {
        await using var db = Db();
        var wallet = await db.MerchantWallets.SingleAsync(x => x.MerchantId == merchantId && x.CurrencyCode == "ETB");
        db.Entry(wallet).Property(nameof(MerchantWallet.AvailableBalance)).CurrentValue = balance;
        await db.SaveChangesAsync();
    }

    private async Task SetBusinessTypeMinimumAsync(string businessType, decimal minimum)
    {
        await using var db = Db();
        var now = DateTime.UtcNow;
        var version = (await db.BusinessTypeWalletMinimumVersions.Where(x => x.CurrencyCode == "ETB" && x.BusinessType == businessType).MaxAsync(x => (int?)x.VersionNumber)) ?? 0;
        db.BusinessTypeWalletMinimumVersions.Add(new BusinessTypeWalletMinimumVersion
        {
            Id = Guid.NewGuid(),
            CurrencyCode = "ETB",
            BusinessType = businessType,
            VersionNumber = version + 1,
            MinimumBusinessWalletBalance = minimum,
            EffectiveFromUtc = now,
            ChangedByUserId = Guid.Parse("90000000-0000-0000-0000-000000000001"),
            CreatedAtUtc = now,
            CreatedBy = "90000000-0000-0000-0000-000000000001"
        });
        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> Get(HttpClient client, string path) => await client.GetAsync(path);
    private async Task<HttpResponseMessage> Post(HttpClient client, string path, object body) => await client.PostAsJsonAsync(path, body);
    private async Task<HttpResponseMessage> Put(HttpClient client, string path, object body) => await client.PutAsJsonAsync(path, body);
    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options);
}
