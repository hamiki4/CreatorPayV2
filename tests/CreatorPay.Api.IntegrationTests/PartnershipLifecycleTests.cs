using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class PartnershipLifecycleTests : IAsyncLifetime
{
    private const string SigningKey = "development-only-replace-this-signing-key-000000";
    private static readonly Guid MerchantId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid MerchantUserId = Guid.Parse("20000000-0000-0000-0000-000000000008");
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("creatorpay_partnership_lifecycle").WithUsername("creatorpay").WithPassword("test-only-password").Build();
    private WebApplicationFactory<Program>? factory;
    private int sequence;

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
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Test"); foreach (var value in values) builder.UseSetting(value.Key, value.Value); builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values)); });
        await using var db = Db();
        await db.Database.MigrateAsync();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
    }

    public async Task DisposeAsync() { if (factory is not null) await factory.DisposeAsync(); await database.DisposeAsync(); }

    [DockerFact]
    public async Task Approved_funded_pair_becomes_active_and_is_removed_from_request_discovery()
    {
        await using (var db = Db())
        {
            foreach (var location in await db.MerchantLocations.Where(x => x.MerchantId == MerchantId).ToListAsync()) location.IsActive = false;
            await db.SaveChangesAsync();
        }
        var creator = await AddCreator("Sammy Regression");
        using var client = Client(creator.UserId, creator.CreatorId);
        var requested = await Post(client, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Sammy requests ABC" });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var request = await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var partnershipId = request.GetProperty("id").GetGuid();

        using var merchant = Client(MerchantUserId, null, MerchantId);
        var approved = await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approvedBody = await approved.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(approvedBody.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("Active", approvedBody.GetProperty("relationshipState").GetString());
        Assert.False(approvedBody.GetProperty("activationRequired").GetBoolean());

        await using (var db = Db())
        {
            var relationship = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == partnershipId);
            Assert.Equal(PartnershipStatus.Approved, relationship.Status);
            Assert.Single(await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == partnershipId && x.Status == CampaignStatus.Active).ToListAsync());
            Assert.False(await db.MerchantLocations.AnyAsync(x => x.MerchantId == MerchantId && x.IsActive));
        }
        var merchantDiscovery = await Get(merchant, "/api/v1/merchant/creators/search?q=Sammy");
        var merchantResults = await merchantDiscovery.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(merchantResults.EnumerateArray(), x => x.GetProperty("id").GetGuid() == creator.CreatorId);
        var creatorDiscovery = await Get(client, "/api/v1/creator/merchants/search?q=Active E2E");
        var creatorResults = await creatorDiscovery.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(creatorResults.EnumerateArray(), x => x.GetProperty("id").GetGuid() == MerchantId);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = (string?)null })).StatusCode);
    }

    [DockerFact]
    public async Task Previously_approved_funded_pair_without_an_active_location_reconciles_to_active()
    {
        var creator = await AddCreator("Legacy Sammy Regression");
        Guid partnershipId;
        await using (var db = Db())
        {
            foreach (var location in await db.MerchantLocations.Where(x => x.MerchantId == MerchantId).ToListAsync()) location.IsActive = false;
            var partnership = new MerchantCreatorPartnership { Id = Guid.NewGuid(), MerchantId = MerchantId, CreatorId = creator.CreatorId, RequestedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow, RequestedByUserId = creator.UserId };
            partnership.Approve(DateTime.UtcNow, MerchantUserId);
            db.MerchantCreatorPartnerships.Add(partnership);
            await db.SaveChangesAsync();
            partnershipId = partnership.Id;
        }

        using var merchant = Client(MerchantUserId, null, MerchantId);
        var reconciled = await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/reconcile-readiness", new { });
        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode);
        var body = await reconciled.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("Active", body.GetProperty("relationshipState").GetString());
        Assert.False(body.GetProperty("activationRequired").GetBoolean());
        await using var verify = Db();
        Assert.Single(await verify.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == partnershipId && x.Status == CampaignStatus.Active).ToListAsync());
        Assert.False(await verify.MerchantLocations.AnyAsync(x => x.MerchantId == MerchantId && x.IsActive));
    }

    [DockerFact]
    public async Task Insufficient_funding_or_a_suspended_merchant_still_requires_activation()
    {
        var creator = await AddCreator("Readiness Guard Creator");
        Guid partnershipId;
        await using (var db = Db())
        {
            var wallet = await db.MerchantWallets.SingleAsync(x => x.MerchantId == MerchantId);
            var settings = await db.PlatformFinancialSettings.SingleAsync();
            settings.MinimumBusinessWalletBalance = wallet.AvailableBalance + 1m;
            var partnership = new MerchantCreatorPartnership { Id = Guid.NewGuid(), MerchantId = MerchantId, CreatorId = creator.CreatorId, RequestedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow, RequestedByUserId = creator.UserId };
            partnership.Approve(DateTime.UtcNow, MerchantUserId);
            db.MerchantCreatorPartnerships.Add(partnership);
            await db.SaveChangesAsync();
            partnershipId = partnership.Id;
        }
        using var merchant = Client(MerchantUserId, null, MerchantId);
        var underfunded = await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/reconcile-readiness", new { });
        Assert.Equal(HttpStatusCode.OK, underfunded.StatusCode);
        Assert.True((await underfunded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("activationRequired").GetBoolean());
        await using (var db = Db())
        {
            var merchantRecord = await db.Merchants.SingleAsync(x => x.Id == MerchantId);
            merchantRecord.Status = MerchantStatus.Suspended;
            await db.SaveChangesAsync();
        }
        var suspended = await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/reconcile-readiness", new { });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        Assert.True((await suspended.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("activationRequired").GetBoolean());
        await using var verify = Db();
        Assert.False(await verify.CreatorMerchantCampaigns.AnyAsync(x => x.MerchantCreatorPartnershipId == partnershipId && x.Status == CampaignStatus.Active));
    }

    [DockerFact]
    public async Task Underfunded_business_cannot_discover_or_invite_creators_and_wallet_reports_configured_minimum()
    {
        var creator = await AddCreator("Underfunded Guard Creator");
        using var merchant = Client(MerchantUserId, null, MerchantId);
        await using (var db = Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 0 WHERE \"MerchantId\" = {MerchantId}");
            var settings = await db.PlatformFinancialSettings.SingleAsync();
            settings.MinimumBusinessWalletBalance = 1000m;
            await db.SaveChangesAsync();
        }
        var walletResponse = await Get(merchant, "/api/v1/merchant/wallet");
        Assert.Equal(HttpStatusCode.OK, walletResponse.StatusCode);
        var walletBody = await walletResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(walletBody.GetProperty("advertisingEligible").GetBoolean());
        Assert.Equal(1000m, walletBody.GetProperty("minimumRequiredBalance").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await Get(merchant, "/api/v1/merchant/creators/search?q=Underfunded")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(merchant, "/api/v1/merchant/partnerships/invitations", new { creatorId = creator.CreatorId, introductoryMessage = "blocked" })).StatusCode);
        await using (var db = Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 2000 WHERE \"MerchantId\" = {MerchantId}");
            (await db.PlatformFinancialSettings.SingleAsync()).MinimumBusinessWalletBalance = 1000m;
            await db.SaveChangesAsync();
        }
    }

    [DockerFact]
    public async Task Declined_requests_are_preserved_and_request_again_creates_one_new_pending_request_and_notification()
    {
        var creator = await AddCreator("Request Again Creator");
        using var client = Client(creator.UserId, creator.CreatorId);
        using var merchant = Client(MerchantUserId, null, MerchantId);
        var first = await Post(client, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = (string?)null });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{firstId}/reject", new { reason = "Declined" })).StatusCode);
        var second = await Post(client, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Request again" });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondId = (await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = (string?)null })).StatusCode);
        await using var db = Db();
        var rows = await db.MerchantCreatorPartnerships.Where(x => x.CreatorId == creator.CreatorId && x.MerchantId == MerchantId).OrderBy(x => x.RequestedAtUtc).ToListAsync();
        Assert.Equal(2, rows.Count); Assert.Equal(PartnershipStatus.Rejected, rows[0].Status); Assert.Equal(PartnershipStatus.Pending, rows[1].Status);
        var keys = new[] { $"partnership:{firstId}:requested", $"partnership:{secondId}:requested" };
        Assert.Equal(2, await db.Notifications.CountAsync(x => keys.Contains(x.IdempotencyKey)));
        Assert.Equal(2, await db.NotificationOutboxMessages.CountAsync(x => keys.Contains(x.Notification.IdempotencyKey)));
    }

    [DockerFact]
    public async Task Business_invitation_decline_can_be_requested_again_while_blocked_pair_cannot()
    {
        var creator = await AddCreator("Business Request Again Creator");
        using var merchant = Client(MerchantUserId, null, MerchantId);
        var first = await Post(merchant, "/api/v1/merchant/partnerships/invitations", new { creatorId = creator.CreatorId, introductoryMessage = (string?)null });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        using var client = Client(creator.UserId, creator.CreatorId);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/creator/partnerships/{firstId}/decline-invitation", new { })).StatusCode);
        var second = await Post(merchant, "/api/v1/merchant/partnerships/invitations", new { creatorId = creator.CreatorId, introductoryMessage = "Request again" });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondId = (await second.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await Post(merchant, "/api/v1/merchant/partnerships/invitations", new { creatorId = creator.CreatorId, introductoryMessage = (string?)null })).StatusCode);
        await using (var verify = Db())
        {
            var rows = await verify.MerchantCreatorPartnerships.Where(x => x.CreatorId == creator.CreatorId && x.MerchantId == MerchantId).OrderBy(x => x.RequestedAtUtc).ToListAsync();
            Assert.Equal(new[] { PartnershipStatus.Rejected, PartnershipStatus.Pending }, rows.Select(x => x.Status));
            var keys = new[] { $"partnership:{firstId}:invited", $"partnership:{secondId}:invited" };
            Assert.Equal(2, await verify.Notifications.CountAsync(x => keys.Contains(x.IdempotencyKey)));
            Assert.Equal(2, await verify.NotificationOutboxMessages.CountAsync(x => keys.Contains(x.Notification.IdempotencyKey)));
        }

        var blocked = await AddCreator("Blocked Creator");
        await using (var db = Db())
        {
            var relationship = new MerchantCreatorPartnership { Id = Guid.NewGuid(), MerchantId = MerchantId, CreatorId = blocked.CreatorId, RequestedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow };
            relationship.Approve(DateTime.UtcNow, MerchantUserId); relationship.Block(DateTime.UtcNow, MerchantUserId); db.MerchantCreatorPartnerships.Add(relationship); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Post(merchant, "/api/v1/merchant/partnerships/invitations", new { creatorId = blocked.CreatorId, introductoryMessage = (string?)null })).StatusCode);
    }

    [DockerFact]
    public async Task Deactivated_approved_relationship_reactivates_without_a_new_request_or_location_and_notifies_once()
    {
        var creator = await AddCreator("Reactivate Regression Creator");
        using var creatorClient = Client(creator.UserId, creator.CreatorId);
        using var merchant = Client(MerchantUserId, null, MerchantId);
        await using (var db = Db())
        {
            foreach (var location in await db.MerchantLocations.Where(x => x.MerchantId == MerchantId).ToListAsync()) location.IsActive = false;
            await db.SaveChangesAsync();
        }

        var requested = await Post(creatorClient, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Reactivate me" });
        var partnershipId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/suspend", new { reason = "Paused" })).StatusCode);

        var reactivated = await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/reactivate", new { reason = "Resume" });
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);
        var result = await reactivated.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(result.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("Active", result.GetProperty("relationshipState").GetString());

        await using (var verify = Db())
        {
            Assert.Single(await verify.MerchantCreatorPartnerships.Where(x => x.Id == partnershipId).ToListAsync());
            Assert.Equal(PartnershipStatus.Approved, (await verify.MerchantCreatorPartnerships.SingleAsync(x => x.Id == partnershipId)).Status);
            Assert.Single(await verify.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == partnershipId && x.Status == CampaignStatus.Active).ToListAsync());
            Assert.False(await verify.MerchantLocations.AnyAsync(x => x.MerchantId == MerchantId && x.IsActive));
            Assert.Single(await verify.Notifications.Where(x => x.IdempotencyKey.StartsWith($"partnership:{partnershipId}:merchant:reactivated:")).ToListAsync());
            Assert.Single(await verify.NotificationOutboxMessages.Where(x => x.Notification.IdempotencyKey.StartsWith($"partnership:{partnershipId}:merchant:reactivated:")).ToListAsync());
        }

        var discovery = await Get(merchant, "/api/v1/merchant/creators/search?q=Reactivate");
        var rows = await discovery.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(rows.EnumerateArray(), x => x.GetProperty("id").GetGuid() == creator.CreatorId);
    }

    private async Task<(Guid CreatorId, Guid UserId)> AddCreator(string name)
    {
        var n = Interlocked.Increment(ref sequence); var creatorId = Guid.NewGuid(); var userId = Guid.NewGuid(); var phone = $"+251988{n:000000}";
        await using var db = Db();
        db.Creators.Add(new Creator { Id = creatorId, PublicCreatorId = $"CR-TEST-{n:N0}", CreatorCode = (4000 + n).ToString(), DisplayName = name, PhoneNumber = phone, NormalizedPhoneNumber = phone, City = "Addis Ababa", Status = CreatorStatus.Active, CreatedAtUtc = DateTime.UtcNow });
        db.UserAccounts.Add(new UserAccount { Id = userId, PhoneNumber = phone, NormalizedPhoneNumber = phone, Role = UserRole.Creator, Status = AccountStatus.Active, CreatorId = creatorId, IsPhoneVerified = true, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(); return (creatorId, userId);
    }

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options);
    private HttpClient Client(Guid userId, Guid? creatorId, Guid? merchantId = null)
    {
        var client = factory!.CreateClient(); var role = merchantId.HasValue ? UserRole.MerchantAdmin : UserRole.Creator;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Role, role.ToString()), new(AuthenticationClaimTypes.AccountStatus, AccountStatus.Active.ToString()), new("test_token", "true") };
        if (creatorId.HasValue) claims.Add(new("creator_id", creatorId.Value.ToString())); if (merchantId.HasValue) claims.Add(new("merchant_id", merchantId.Value.ToString()));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return client;
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body) => client.PostAsJsonAsync(path, body);
    private static Task<HttpResponseMessage> Get(HttpClient client, string path) => client.GetAsync(path);
}
