using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Checkout;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class RepeatUseOverrideFinancialTests : IAsyncLifetime
{
    private const string SigningKey = "development-only-replace-this-signing-key-000000";
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("creatorpay_override_tests").WithUsername("creatorpay").WithPassword("test-only-password").Build();
    private WebApplicationFactory<Program>? factory;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("Authentication:Jwt:SigningKey", SigningKey);
            builder.UseSetting("ConnectionStrings:CreatorPayDatabase", container.GetConnectionString());
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CreatorPayDatabase"] = container.GetConnectionString(),
                ["Authentication:Jwt:SigningKey"] = SigningKey,
                ["CustomerVerification:HmacSecret"] = "test-only-hmac-secret-0000000000000000000000",
                ["CustomerVerification:EncryptionKey"] = "test-only-encryption-key-000000000000000000",
                ["Cors:AllowedOrigins:0"] = "http://127.0.0.1"
            }));
        });
        await using var db = Db();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        await container.DisposeAsync();
    }

    [DockerFact]
    public async Task Reusable_offer_qr_checkout_validates_phone_and_posts_exactly_once()
    {
        using var client = factory!.CreateClient();
        var seedResponse = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seedResponse.StatusCode);
        var seed = await seedResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var location = seed.GetProperty("locationId").GetGuid();
        var payload = seed.GetProperty("offerQrPayload").GetString()!;
        var approvedCreator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var unapprovedCreator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000099", creatorId: "30000000-0000-0000-0000-000000000001", status: AccountStatus.PendingApproval);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/v1/creator/campaigns/60000000-0000-0000-0000-000000000001/qr-image", approvedCreator)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/v1/creator/campaigns/60000000-0000-0000-0000-000000000001/qr-image", unapprovedCreator)).StatusCode);

        var unknown = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = payload, merchantLocationId = location, shopperPhoneNumber = "0911 999 999", purchaseAmount = 100m }, "unknown-shopper");
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal("shopper_not_registered", (await unknown.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());
        Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot());

        var first = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = payload, merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "direct-once");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("completed", result.GetProperty("code").GetString());
        Assert.Equal("09*****001", result.GetProperty("maskedPhoneNumber").GetString());
        var afterFirst = await Snapshot();
        Assert.Equal((1, 1, 1, 1, 1, 1, 1), afterFirst);

        var duplicate = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = payload, merchantLocationId = location, shopperPhoneNumber = "+251911000001", purchaseAmount = 100m }, "direct-once");
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(afterFirst, await Snapshot());
        await using (var db = Db()) Assert.True(await db.Notifications.CountAsync() >= 2);

        await using (var db = Db()) { var merchant = await db.Merchants.SingleAsync(x => x.Id == Guid.Parse("20000000-0000-0000-0000-000000000001")); merchant.Status = MerchantStatus.PendingReview; await db.SaveChangesAsync(); }
        var beforeUnapprovedBusiness = await Snapshot();
        var unapprovedBusiness = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = payload, merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "unapproved-business");
        Assert.Equal(HttpStatusCode.Conflict, unapprovedBusiness.StatusCode);
        Assert.Equal(beforeUnapprovedBusiness, await Snapshot());
        await using (var db = Db()) { var merchant = await db.Merchants.SingleAsync(x => x.Id == Guid.Parse("20000000-0000-0000-0000-000000000001")); merchant.Status = MerchantStatus.Active; await db.SaveChangesAsync(); }

        var expired = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("expiredOfferQrPayload").GetString(), merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "expired-offer");
        Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
        var invalid = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = payload + "tampered", merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "invalid-offer");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await using (var db = Db()) { var wallet = await db.MerchantWallets.SingleAsync(); wallet.Debit(wallet.AvailableBalance, 0, DateTime.UtcNow); await db.SaveChangesAsync(); }
        var insufficient = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = payload, merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "insufficient-wallet");
        Assert.Equal("insufficient_wallet", (await insufficient.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());
        await using (var db = Db()) { var qr = await db.CampaignQrCodes.SingleAsync(x => x.PublicQrId == seed.GetProperty("offerQrId").GetString()); qr.Revoke(DateTime.UtcNow); await db.SaveChangesAsync(); }
        var disabled = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = payload, merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "disabled-offer");
        Assert.Equal(HttpStatusCode.Conflict, disabled.StatusCode);
    }

    [DockerFact]
    public async Task Approved_override_posts_financials_once_and_unapproved_duplicate_posts_nothing()
    {
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);

        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");

        var first = await CreateAndPresent(client, shopper, cashier, "first");
        await Approve(first, "approve-first");

        var duplicate = await CreateAndPresent(client, shopper, cashier, "duplicate");
        var beforeRejected = await Snapshot();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Approve(duplicate, "approve-duplicate"));
        Assert.Equal(beforeRejected, await Snapshot());

        var requestResponse = await Post(client, "/api/v1/cashier/checkouts/repeat-use-approvals", cashier, new { checkoutId = duplicate, reason = "Customer requested a supervised repeat purchase" }, "override-request");
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);
        var approvalId = (await requestResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/repeat-use-approvals/{approvalId}/approve", owner, new { reason = "Owner verified the current checkout" })).StatusCode);
        await Approve(duplicate, "approve-with-override");

        await using (var db = Db())
        {
            Assert.Equal(2, await db.PurchaseTransactions.CountAsync());
            Assert.Equal(2, await db.MerchantWalletEntries.CountAsync(x => x.EntryType == MerchantWalletEntryType.CommissionDebit));
            Assert.Equal(2, await db.CreatorEarnings.CountAsync());
            Assert.Equal(2, await db.CustomerCashbackEntries.CountAsync(x => x.EntryType == CustomerCashbackEntryType.Earned));
            Assert.Equal(2, await db.CommissionCalculationSnapshots.CountAsync());
            Assert.Equal(2, await db.PlatformRevenueEntries.CountAsync());
            Assert.Equal(2, await db.FinancialJournals.CountAsync(x => x.IsPosted));
            Assert.Equal(8, await db.FinancialJournalLines.CountAsync());
            Assert.Equal(980m, await db.MerchantWallets.Select(x => x.AvailableBalance).SingleAsync());
            Assert.Equal(8m, await db.CreatorBalanceAccounts.Select(x => x.PendingBalance).SingleAsync());
            Assert.Equal(6m, await db.CustomerWallets.Select(x => x.AvailableCashback).SingleAsync());
            var approval = await db.RepeatUseApprovalRequests.SingleAsync(x => x.Id == approvalId);
            Assert.Equal(RepeatUseApprovalStatus.Approved, approval.Status);
            Assert.NotNull(approval.FinalizedAtUtc);
        }

        var third = await CreateAndPresent(client, shopper, cashier, "third");
        var beforeReuse = await Snapshot();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Approve(third, "reuse-consumed-override"));
        Assert.Equal(beforeReuse, await Snapshot());
    }

    private async Task<Guid> CreateAndPresent(HttpClient client, string shopper, string cashier, string key)
    {
        var create = await Post(client, "/api/v1/customer/checkouts/by-offer", shopper, new { offerCode = "E2EACTIVE" }, $"create-{key}");
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var checkout = await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = checkout.GetProperty("id").GetGuid();
        await using var db = Db();
        var row = await db.CheckoutSessions.SingleAsync(x => x.Id == id);
        row.PurchaseAmount = 100m;
        row.ExpectedCreatorAmount = 4m;
        row.ExpectedCashbackAmount = 3m;
        row.CashierId = Guid.Parse("20000000-0000-0000-0000-000000000005");
        row.MerchantLocationId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        row.PresentedAtUtc = DateTime.UtcNow;
        row.PresentIdempotencyKey = $"present-{key}";
        row.Status = CheckoutSessionStatus.AwaitingCustomerApproval;
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<(int Purchases, int WalletEntries, int Earnings, int Cashback, int Snapshots, int Revenue, int Journals)> Snapshot()
    {
        await using var db = Db();
        return (await db.PurchaseTransactions.CountAsync(), await db.MerchantWalletEntries.CountAsync(), await db.CreatorEarnings.CountAsync(), await db.CustomerCashbackEntries.CountAsync(), await db.CommissionCalculationSnapshots.CountAsync(), await db.PlatformRevenueEntries.CountAsync(), await db.FinancialJournals.CountAsync());
    }

    private async Task Approve(Guid checkoutId, string key)
    {
        await using var scope = factory!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICheckoutService>().ApproveAsync(Guid.Parse("10000000-0000-0000-0000-000000000001"), checkoutId, key, CancellationToken.None);
    }

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(container.GetConnectionString()).Options);

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string token, object body, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> Get(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
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
}
