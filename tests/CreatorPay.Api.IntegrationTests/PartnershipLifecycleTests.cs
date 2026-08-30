using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CreatorPay.Api.Partnerships;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    public async Task Approved_funded_pair_grants_permission_without_starting_a_campaign()
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
        Assert.False(approvedBody.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("AwaitingVideo", approvedBody.GetProperty("relationshipState").GetString());
        Assert.True(approvedBody.GetProperty("activationRequired").GetBoolean());

        await using (var db = Db())
        {
            var relationship = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == partnershipId);
            Assert.Equal(PartnershipStatus.Approved, relationship.Status);
            Assert.Empty(await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == partnershipId).ToListAsync());
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
    public async Task Previously_approved_funded_pair_reconcile_does_not_publish_without_creator_go_live()
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
        Assert.False(body.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("AwaitingVideo", body.GetProperty("relationshipState").GetString());
        Assert.True(body.GetProperty("activationRequired").GetBoolean());
        await using var verify = Db();
        Assert.Empty(await verify.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == partnershipId).ToListAsync());
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
            await SetBusinessTypeMinimumAsync("Other", wallet.AvailableBalance + 1m);
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
            await SetBusinessTypeMinimumAsync("Other", 1000m);
            await db.SaveChangesAsync();
        }
        var walletResponse = await Get(merchant, "/api/v1/merchant/wallet");
        Assert.Equal(HttpStatusCode.OK, walletResponse.StatusCode);
        var walletBody = await walletResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(walletBody.GetProperty("advertisingEligible").GetBoolean());
        Assert.Equal(1000m, walletBody.GetProperty("minimumRequiredBalance").GetDecimal());
        Assert.Equal(HttpStatusCode.Conflict, (await Get(merchant, "/api/v1/merchant/creators/search?q=Underfunded")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(merchant, "/api/v1/merchant/partnerships/invitations", new { creatorId = creator.CreatorId, introductoryMessage = "blocked" })).StatusCode);
        var seededCreator = Client(Guid.Parse("30000000-0000-0000-0000-000000000004"), Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var shopper = CustomerClient(Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var creatorRows = await (await Get(seededCreator, "/api/v1/creator/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var activeRow = Assert.Single(creatorRows!.EnumerateArray(), x => x.GetProperty("merchantName").GetString() == "Active E2E Business");
        Assert.False(activeRow.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("Approved", activeRow.GetProperty("relationshipState").GetString());
        var shopperRows = await (await Get(shopper, "/api/v1/customer/discovery/advertising")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(shopperRows!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");
        var shopperBusinesses = await (await Get(shopper, "/api/v1/customer/discovery/businesses?q=Addis")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(shopperBusinesses!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");
        await using (var db = Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 2000 WHERE \"MerchantId\" = {MerchantId}");
            await SetBusinessTypeMinimumAsync("Other", 1000m);
            await db.SaveChangesAsync();
        }
        creatorRows = await (await Get(seededCreator, "/api/v1/creator/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        activeRow = Assert.Single(creatorRows!.EnumerateArray(), x => x.GetProperty("merchantName").GetString() == "Active E2E Business");
        Assert.True(activeRow.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("Active", activeRow.GetProperty("relationshipState").GetString());
        shopperRows = await (await Get(shopper, "/api/v1/customer/discovery/advertising")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(shopperRows!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");
        shopperBusinesses = await (await Get(shopper, "/api/v1/customer/discovery/businesses?q=Addis")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(shopperBusinesses!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");
    }

    [DockerFact]
    public async Task Creator_find_businesses_hides_underfunded_businesses_and_reveals_them_again_after_restocking()
    {
        var creator = await AddCreator("Search Visibility Creator");
        using var client = Client(creator.UserId, creator.CreatorId);
        await using (var db = Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 2000 WHERE \"MerchantId\" = {MerchantId}");
            await SetBusinessTypeMinimumAsync("Other", 1000m);
            await db.SaveChangesAsync();
        }
        var funded = await Get(client, "/api/v1/creator/merchants/search?q=Active E2E");
        Assert.Equal(HttpStatusCode.OK, funded.StatusCode);
        var fundedRows = await funded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(fundedRows!.EnumerateArray(), x => x.GetProperty("id").GetGuid() == MerchantId);

        await using (var db = Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 350 WHERE \"MerchantId\" = {MerchantId}");
            await SetBusinessTypeMinimumAsync("Other", 1000m);
            await db.SaveChangesAsync();
        }
        var underfunded = await Get(client, "/api/v1/creator/merchants/search?q=Active E2E");
        Assert.Equal(HttpStatusCode.OK, underfunded.StatusCode);
        var underfundedRows = await underfunded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(underfundedRows!.EnumerateArray(), x => x.GetProperty("id").GetGuid() == MerchantId);

        await using (var db = Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 1000 WHERE \"MerchantId\" = {MerchantId}");
            await db.SaveChangesAsync();
        }
        var restored = await Get(client, "/api/v1/creator/merchants/search?q=Active E2E");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var restoredRows = await restored.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(restoredRows!.EnumerateArray(), x => x.GetProperty("id").GetGuid() == MerchantId);
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
    public async Task Creator_go_live_starts_period_and_customer_visibility_only_after_video_approval()
    {
        var creator = await AddCreator("Promo Video Creator");
        using var creatorClient = Client(creator.UserId, creator.CreatorId);
        using var merchant = Client(MerchantUserId, null, MerchantId);
        using var customer = CustomerClient(Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000001"));
        const string videoUrl = "https://www.tiktok.com/@weymela/video/1234567890123456789?lang=en";

        var requested = await Post(creatorClient, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Promo submission" });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var partnershipId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();

        var merchantRequests = await (await Get(merchant, "/api/v1/merchant/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var creatorRequest = Assert.Single(merchantRequests.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId);
        Assert.Equal("Pending", creatorRequest.GetProperty("status").GetString());
        Assert.Equal("Creator", creatorRequest.GetProperty("initiatedBy").GetString());

        var permission = await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" });
        Assert.Equal(HttpStatusCode.OK, permission.StatusCode);
        var permissionBody = await permission.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("AwaitingVideo", permissionBody.GetProperty("relationshipState").GetString());
        Assert.False(permissionBody.GetProperty("promotionActive").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, permissionBody.GetProperty("activatedAtUtc").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, permissionBody.GetProperty("expiresAtUtc").ValueKind);
        merchantRequests = await (await Get(merchant, "/api/v1/merchant/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(merchantRequests.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId && x.GetProperty("status").GetString() == "Pending");

        var beforeVideo = await Get(customer, "/api/v1/customer/discovery/advertising?q=Promo");
        var beforeVideoRows = await beforeVideo.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(beforeVideoRows!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");

        var submitted = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var pending = await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Pending", pending.GetProperty("status").GetString());
        Assert.Equal(videoUrl, pending.GetProperty("videoUrl").GetString());

        var merchantVideoApprovals = await (await Get(merchant, "/api/v1/merchant/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var pendingVideoApproval = Assert.Single(merchantVideoApprovals.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId && x.GetProperty("promotionVideo").GetProperty("status").GetString() == "Pending");
        Assert.Equal(videoUrl, pendingVideoApproval.GetProperty("promotionVideo").GetProperty("videoUrl").GetString());

        var pendingRelationships = await (await Get(creatorClient, "/api/v1/creator/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var pendingRelationship = pendingRelationships.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == partnershipId);
        Assert.Equal("PendingApproval", pendingRelationship.GetProperty("relationshipState").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, pendingRelationship.GetProperty("activatedAtUtc").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, pendingRelationship.GetProperty("expiresAtUtc").ValueKind);

        var duplicate = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl });
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(pending.GetProperty("id").GetGuid(), duplicateBody.GetProperty("id").GetGuid());
        Assert.Equal("Pending", duplicateBody.GetProperty("status").GetString());
        await using (var submittedCheck = Db())
            Assert.Single(await submittedCheck.Notifications.Where(x => x.IdempotencyKey == $"promotion-video:{pending.GetProperty("id").GetGuid()}:submitted").ToListAsync());

        var hidden = await Get(customer, "/api/v1/customer/discovery/advertising?q=Promo");
        Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
        var hiddenRows = await hidden.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(hiddenRows!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");

        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/promotion-videos/{pending.GetProperty("id").GetGuid()}/approve", new { reason = (string?)null })).StatusCode);
        await using (var approvedCheck = Db())
            Assert.Single(await approvedCheck.Notifications.Where(x => x.IdempotencyKey == $"promotion-video:{pending.GetProperty("id").GetGuid()}:approved").ToListAsync());
        var approvedRelationships = await (await Get(creatorClient, "/api/v1/creator/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var approvedRelationship = approvedRelationships.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == partnershipId);
        Assert.Equal("Approved", approvedRelationship.GetProperty("relationshipState").GetString());
        Assert.Equal("Approved", approvedRelationship.GetProperty("promotionVideo").GetProperty("status").GetString());
        Assert.False(approvedRelationship.GetProperty("promotionActive").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, approvedRelationship.GetProperty("activatedAtUtc").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, approvedRelationship.GetProperty("expiresAtUtc").ValueKind);
        merchantVideoApprovals = await (await Get(merchant, "/api/v1/merchant/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(merchantVideoApprovals.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId && x.GetProperty("promotionVideo").GetProperty("status").GetString() == "Pending");

        var stillHidden = await Get(customer, "/api/v1/customer/discovery/advertising?q=Promo");
        var stillHiddenRows = await stillHidden.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(stillHiddenRows!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");

        var creatorProfile = await (await Get(creatorClient, "/api/v1/creators/me")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Active", creatorProfile.GetProperty("effectiveStatus").GetString());

        await using (var db = Db())
        {
            var account = await db.UserAccounts.SingleAsync(x => x.Id == creator.UserId);
            account.LockoutEndUtc = DateTime.UtcNow.AddMinutes(5);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/go-live", new { })).StatusCode);
        await using (var db = Db())
        {
            var account = await db.UserAccounts.SingleAsync(x => x.Id == creator.UserId);
            account.LockoutEndUtc = null;
            var merchantRow = await db.Merchants.SingleAsync(x => x.Id == MerchantId);
            merchantRow.Status = MerchantStatus.Suspended;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/go-live", new { })).StatusCode);
        await using (var db = Db())
        {
            var merchantRow = await db.Merchants.SingleAsync(x => x.Id == MerchantId);
            merchantRow.Status = MerchantStatus.Active;
            Assert.False(await db.CreatorMerchantCampaigns.AnyAsync(x => x.MerchantCreatorPartnershipId == partnershipId));
            await db.SaveChangesAsync();
        }

        var beforeGoLive = DateTime.UtcNow;
        var goLive = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/go-live", new { });
        Assert.Equal(HttpStatusCode.OK, goLive.StatusCode);
        var liveBody = await goLive.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(liveBody.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("Active", liveBody.GetProperty("relationshipState").GetString());
        Assert.Equal("Live", liveBody.GetProperty("promotionVideo").GetProperty("status").GetString());
        var activatedAt = liveBody.GetProperty("activatedAtUtc").GetDateTime();
        var expiresAt = liveBody.GetProperty("expiresAtUtc").GetDateTime();
        Assert.InRange(activatedAt, beforeGoLive, DateTime.UtcNow);
        Assert.Equal(activatedAt.AddDays(MerchantCreatorPartnership.ActivePeriodDays), expiresAt);

        var merchantActiveAds = await (await Get(merchant, "/api/v1/merchant/partnerships")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var activeAd = Assert.Single(merchantActiveAds.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId && x.GetProperty("relationshipState").GetString() == "Active");
        Assert.Equal(videoUrl, activeAd.GetProperty("promotionVideo").GetProperty("videoUrl").GetString());

        var live = await Get(customer, "/api/v1/customer/discovery/advertising?q=Promo");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        var liveRows = await live.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var row = Assert.Single(liveRows!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");
        Assert.Equal(videoUrl, row.GetProperty("promotionVideoUrl").GetString());
        Assert.Equal("Live", row.GetProperty("promotionVideoStatus").GetString());
        Assert.Equal("TikTok", row.GetProperty("promotionVideoPlatform").GetString());

        var located = await Get(customer, "/api/v1/customer/discovery/advertising?q=Promo&latitude=9.03&longitude=38.74");
        Assert.Equal(HttpStatusCode.OK, located.StatusCode);
        var locatedRows = await located.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var locatedRow = Assert.Single(locatedRows!.EnumerateArray(), x => x.GetProperty("relationshipId").GetGuid() == partnershipId);
        Assert.Equal(9.03, locatedRow.GetProperty("businessLatitude").GetDouble(), 3);
        Assert.Equal(38.74, locatedRow.GetProperty("businessLongitude").GetDouble(), 3);
        Assert.InRange(locatedRow.GetProperty("distanceKm").GetDouble(), 0, 0.01);
    }

    [DockerFact]
    public async Task Exact_tiktok_t_share_link_enters_business_approval_and_stays_hidden_until_creator_go_live()
    {
        const string shareUrl = "https://www.tiktok.com/t/ZTD3GnwFT/";
        const string canonicalUrl = "https://www.tiktok.com/@bealti_shekuar/video/7679446711865036039?_r=1&_t=ZT-99JmDTkTzAM";
        using var shortLinkFactory = factory!.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITikTokVideoUrlResolver>();
            services.AddSingleton<ITikTokVideoUrlResolver>(new TikTokVideoUrlResolver(new HttpClient(new TikTokRedirectHandler(canonicalUrl))));
        }));
        var creator = await AddCreator("Short Share Promo Creator");
        using var creatorClient = Client(creator.UserId, creator.CreatorId, app: shortLinkFactory);
        using var merchant = Client(MerchantUserId, null, MerchantId, shortLinkFactory);
        using var customer = CustomerClient(Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000001"), shortLinkFactory);

        var requested = await Post(creatorClient, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Exact short share link" });
        var partnershipId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" })).StatusCode);

        var submitted = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl = shareUrl });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var pending = await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Pending", pending.GetProperty("status").GetString());
        Assert.Equal(canonicalUrl, pending.GetProperty("videoUrl").GetString());

        var businessRows = await (await Get(merchant, "/api/v1/merchant/promotion-videos")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var businessPending = Assert.Single(businessRows.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId);
        Assert.Equal("Pending", businessPending.GetProperty("promotionVideo").GetProperty("status").GetString());
        Assert.Equal(canonicalUrl, businessPending.GetProperty("promotionVideo").GetProperty("videoUrl").GetString());
        var hiddenBeforeApproval = await (await Get(customer, "/api/v1/customer/discovery/advertising?q=Short%20Share")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Empty(hiddenBeforeApproval.EnumerateArray());

        var videoId = pending.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/promotion-videos/{videoId}/approve", new { reason = (string?)null })).StatusCode);
        var hiddenAfterApproval = await (await Get(customer, "/api/v1/customer/discovery/advertising?q=Short%20Share")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Empty(hiddenAfterApproval.EnumerateArray());

        var beforeGoLive = DateTime.UtcNow;
        var live = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/go-live", new { });
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        var liveBody = await live.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var activatedAt = liveBody.GetProperty("activatedAtUtc").GetDateTime();
        Assert.InRange(activatedAt, beforeGoLive, DateTime.UtcNow);
        Assert.Equal(activatedAt.AddDays(MerchantCreatorPartnership.ActivePeriodDays), liveBody.GetProperty("expiresAtUtc").GetDateTime());
        var visible = await (await Get(customer, "/api/v1/customer/discovery/advertising?q=Short%20Share")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var promotion = Assert.Single(visible.EnumerateArray());
        Assert.Equal(canonicalUrl, promotion.GetProperty("promotionVideoUrl").GetString());
        Assert.Equal("Live", promotion.GetProperty("promotionVideoStatus").GetString());
    }

    [DockerFact]
    public async Task Rejected_promotion_video_preserves_feedback_history_and_can_be_revised_until_go_live()
    {
        var creator = await AddCreator("Rejected Promo Creator");
        using var creatorClient = Client(creator.UserId, creator.CreatorId);
        using var merchant = Client(MerchantUserId, null, MerchantId);
        using var customer = CustomerClient(Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var requested = await Post(creatorClient, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Reject test" });
        var partnershipId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" })).StatusCode);
        var submitted = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl = "https://www.tiktok.com/@weymela/video/2222222222222222222" });
        var videoId = (await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var rejected = await Post(merchant, $"/api/v1/merchant/promotion-videos/{videoId}/reject", new { reason = (string?)null });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Rejected", rejectedBody.GetProperty("status").GetString());
        Assert.Equal("Please contact the business for more information.", rejectedBody.GetProperty("rejectionReason").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/go-live", new { })).StatusCode);
        var rows = await (await Get(customer, "/api/v1/customer/discovery/advertising?q=Rejected")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(rows!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");
        await using (var verify = Db())
        {
            Assert.False(await verify.CreatorMerchantCampaigns.AnyAsync(x => x.MerchantCreatorPartnershipId == partnershipId && x.Status == CampaignStatus.Active));
            var notice = Assert.Single(await verify.Notifications.Where(x => x.IdempotencyKey == $"promotion-video:{videoId}:rejected").ToListAsync());
            Assert.DoesNotContain("Please contact the business", notice.Body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Review the Business feedback", notice.Body, StringComparison.OrdinalIgnoreCase);
        }

        var creatorRejected = await (await Get(creatorClient, "/api/v1/creator/promotion-videos")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var rejectedItem = Assert.Single(creatorRejected.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId);
        Assert.Equal("Rejected", rejectedItem.GetProperty("promotionVideo").GetProperty("status").GetString());
        Assert.Equal("Please contact the business for more information.", rejectedItem.GetProperty("promotionVideo").GetProperty("rejectionReason").GetString());

        const string firstRevisionUrl = "https://www.tiktok.com/@weymela/video/3333333333333333333";
        var firstRevision = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl = firstRevisionUrl });
        Assert.Equal(HttpStatusCode.Created, firstRevision.StatusCode);
        var firstRevisionId = (await firstRevision.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.NotEqual(videoId, firstRevisionId);
        var merchantPending = await (await Get(merchant, "/api/v1/merchant/promotion-videos")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var pendingItem = Assert.Single(merchantPending.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId);
        Assert.Equal("Pending", pendingItem.GetProperty("promotionVideo").GetProperty("status").GetString());
        Assert.Equal(firstRevisionUrl, pendingItem.GetProperty("promotionVideo").GetProperty("videoUrl").GetString());
        Assert.Empty((await (await Get(customer, "/api/v1/customer/discovery/advertising?q=Rejected")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).EnumerateArray());

        const string customReason = "Show the product and price more clearly.";
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/promotion-videos/{firstRevisionId}/reject", new { reason = customReason })).StatusCode);
        var customRejectedRows = await (await Get(creatorClient, "/api/v1/creator/promotion-videos")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var customRejected = Assert.Single(customRejectedRows.EnumerateArray(), x => x.GetProperty("id").GetGuid() == partnershipId);
        Assert.Equal(customReason, customRejected.GetProperty("promotionVideo").GetProperty("rejectionReason").GetString());

        const string approvedReplacementUrl = "https://www.tiktok.com/@weymela/video/4444444444444444444";
        var approvedReplacement = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl = approvedReplacementUrl });
        Assert.Equal(HttpStatusCode.Created, approvedReplacement.StatusCode);
        var approvedReplacementId = (await approvedReplacement.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.NotEqual(firstRevisionId, approvedReplacementId);
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/promotion-videos/{approvedReplacementId}/approve", new { reason = (string?)null })).StatusCode);
        Assert.Empty((await (await Get(customer, "/api/v1/customer/discovery/advertising?q=Rejected")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).EnumerateArray());

        var beforeGoLive = DateTime.UtcNow;
        var live = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/go-live", new { });
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        var liveBody = await live.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var activatedAt = liveBody.GetProperty("activatedAtUtc").GetDateTime();
        Assert.InRange(activatedAt, beforeGoLive, DateTime.UtcNow);
        Assert.Equal(activatedAt.AddDays(MerchantCreatorPartnership.ActivePeriodDays), liveBody.GetProperty("expiresAtUtc").GetDateTime());
        var visibleRows = await (await Get(customer, "/api/v1/customer/discovery/advertising?q=Rejected")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var visible = Assert.Single(visibleRows.EnumerateArray());
        Assert.Equal(approvedReplacementUrl, visible.GetProperty("promotionVideoUrl").GetString());

        await using (var verify = Db())
        {
            var history = await verify.PromotionVideos.Where(x => x.MerchantCreatorPartnershipId == partnershipId).OrderBy(x => x.SubmittedAtUtc).ToListAsync();
            Assert.Equal(3, history.Count);
            Assert.Equal([PromotionVideoStatus.Rejected, PromotionVideoStatus.Rejected, PromotionVideoStatus.Approved], history.Select(x => x.Status).ToArray());
            Assert.Equal("Please contact the business for more information.", history[0].RejectionReason);
            Assert.Equal(customReason, history[1].RejectionReason);
            Assert.Null(history[2].RejectionReason);
            Assert.Single(await verify.Notifications.Where(x => x.IdempotencyKey == $"promotion-video:{firstRevisionId}:rejected").ToListAsync());
            Assert.Single(await verify.Notifications.Where(x => x.IdempotencyKey == $"promotion-video:{approvedReplacementId}:approved").ToListAsync());
        }
    }

    [DockerFact]
    public async Task Invalid_profile_only_tiktok_url_is_rejected_and_wrong_business_cannot_review_promotion_videos()
    {
        var creator = await AddCreator("Invalid Promo Creator");
        using var creatorClient = Client(creator.UserId, creator.CreatorId);
        using var merchant = Client(MerchantUserId, null, MerchantId);
        var wrongMerchant = await AddMerchant("Wrong Promo Reviewer");
        using var wrongMerchantClient = Client(wrongMerchant.UserId, null, wrongMerchant.MerchantId);

        var requested = await Post(creatorClient, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Promo validation" });
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var partnershipId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" })).StatusCode);

        var invalid = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl = "https://www.tiktok.com/@weymela" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("video link", await invalid.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var submitted = await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl = "https://www.tiktok.com/@weymela/video/9876543210987654321" });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var videoId = (await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await Post(wrongMerchantClient, $"/api/v1/merchant/promotion-videos/{videoId}/approve", new { reason = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(creatorClient, $"/api/v1/merchant/promotion-videos/{videoId}/approve", new { reason = (string?)null })).StatusCode);
    }

    [DockerFact]
    public async Task Expired_relationship_hides_customer_promotion_video_but_keeps_history()
    {
        var creator = await AddCreator("Expired Promo Creator");
        using var creatorClient = Client(creator.UserId, creator.CreatorId);
        using var merchant = Client(MerchantUserId, null, MerchantId);
        using var customer = CustomerClient(Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000001"));

        var requested = await Post(creatorClient, "/api/v1/creator/partnerships/requests", new { merchantId = MerchantId, introductoryMessage = "Expired promo" });
        var partnershipId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(merchant, $"/api/v1/merchant/partnerships/{partnershipId}/approve", new { reason = "Approved" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(creatorClient, $"/api/v1/creator/partnerships/{partnershipId}/promotion-video", new { videoUrl = "https://www.tiktok.com/@weymela/video/1111111111111111111" })).StatusCode);
        await using (var db = Db())
        {
            var row = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == partnershipId);
            db.Entry(row).Property(nameof(MerchantCreatorPartnership.EndDateUtc)).CurrentValue = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }

        var discovery = await Get(customer, "/api/v1/customer/discovery/advertising?q=Expired");
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        var rows = await discovery.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(rows!.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business");
        await using var verify = Db();
        Assert.True(await verify.PromotionVideos.AnyAsync(x => x.MerchantCreatorPartnershipId == partnershipId));
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
        Assert.False(result.GetProperty("promotionActive").GetBoolean());
        Assert.Equal("AwaitingVideo", result.GetProperty("relationshipState").GetString());

        await using (var verify = Db())
        {
            Assert.Single(await verify.MerchantCreatorPartnerships.Where(x => x.Id == partnershipId).ToListAsync());
            Assert.Equal(PartnershipStatus.Approved, (await verify.MerchantCreatorPartnerships.SingleAsync(x => x.Id == partnershipId)).Status);
            Assert.Empty(await verify.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == partnershipId).ToListAsync());
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

    private async Task<(Guid MerchantId, Guid UserId)> AddMerchant(string name)
    {
        var n = Interlocked.Increment(ref sequence); var merchantId = Guid.NewGuid(); var userId = Guid.NewGuid(); var phone = $"+251977{n:000000}";
        await using var db = Db();
        db.Merchants.Add(new Merchant
        {
            Id = merchantId,
            PublicMerchantId = $"BM-TEST-{n:N0}",
            LegalBusinessName = name,
            TradingName = name,
            BusinessType = "Other",
            PrimaryContactName = name,
            PhoneNumber = phone,
            NormalizedPhoneNumber = phone,
            PreferredLanguage = "en",
            TermsAcceptedAtUtc = DateTime.UtcNow,
            BusinessAddress = "Addis Ababa",
            City = "Addis Ababa",
            Region = "Addis Ababa",
            Country = "Ethiopia",
            TimeZone = "Africa/Addis_Ababa",
            Status = MerchantStatus.Active
        });
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            PhoneNumber = phone,
            NormalizedPhoneNumber = phone,
            Role = UserRole.MerchantAdmin,
            Status = AccountStatus.Active,
            MerchantId = merchantId,
            IsPhoneVerified = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (merchantId, userId);
    }

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options);
    private HttpClient Client(Guid userId, Guid? creatorId, Guid? merchantId = null, WebApplicationFactory<Program>? app = null)
    {
        var client = (app ?? factory!).CreateClient(); var role = merchantId.HasValue ? UserRole.MerchantAdmin : UserRole.Creator;
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Role, role.ToString()), new(AuthenticationClaimTypes.AccountStatus, AccountStatus.Active.ToString()), new("test_token", "true") };
        if (creatorId.HasValue) claims.Add(new("creator_id", creatorId.Value.ToString())); if (merchantId.HasValue) claims.Add(new("merchant_id", merchantId.Value.ToString()));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return client;
    }
    private HttpClient CustomerClient(Guid userId, Guid customerId, WebApplicationFactory<Program>? app = null)
    {
        var client = (app ?? factory!).CreateClient();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.Role, UserRole.Customer.ToString()), new(AuthenticationClaimTypes.AccountStatus, AccountStatus.Active.ToString()), new("test_token", "true"), new("customer_id", customerId.ToString()) };
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return client;
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string path, object body) => client.PostAsJsonAsync(path, body);
    private static Task<HttpResponseMessage> Get(HttpClient client, string path) => client.GetAsync(path);

    private sealed class TikTokRedirectHandler(string canonicalUrl) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri(canonicalUrl) } });
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
            ChangedByUserId = MerchantUserId,
            CreatedAtUtc = now,
            CreatedBy = MerchantUserId.ToString()
        });
        await db.SaveChangesAsync();
    }
}
