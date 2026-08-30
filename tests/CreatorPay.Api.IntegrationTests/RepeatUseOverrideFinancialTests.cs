using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Checkout;
using CreatorPay.Application.Earnings;
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
            builder.UseSetting("SmsOtp:SmsProvider", "PilotTest");
            builder.UseSetting("SmsOtp:HashSecret", "test-only-phone-otp-secret-000000000000000");
            builder.UseSetting("SmsOtp:TestCode", "654321");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CreatorPayDatabase"] = container.GetConnectionString(),
                ["Authentication:Jwt:SigningKey"] = SigningKey,
                ["CustomerVerification:HmacSecret"] = "test-only-hmac-secret-0000000000000000000000",
                ["CustomerVerification:EncryptionKey"] = "test-only-encryption-key-000000000000000000",
                ["SmsOtp:SmsProvider"] = "PilotTest",
                ["SmsOtp:HashSecret"] = "test-only-phone-otp-secret-000000000000000",
                ["SmsOtp:TestCode"] = "654321",
                ["CreatorPayouts:CreatorEarningHoldingPeriodDays"] = "0",
                ["CreatorPayouts:MinimumCreatorPayoutAmount"] = "0",
                ["Checkout:CashbackPayoutThreshold"] = "0",
                ["DepositProofStorage:RootPath"] = Path.Combine(Path.GetTempPath(), $"creatorpay-proof-tests-{Guid.NewGuid():N}"),
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
    public async Task Full_login_enroll_then_pin_unlock_accepts_local_and_normalized_Ethiopian_phone()
    {
        using var client = factory!.CreateClient();
        const string phone = "+251977199991", password = "Focused-pin-test-1!", pin = "12345";
        var registration = await client.PostAsJsonAsync("/api/v1/customers/register", new { displayName = "Focused PIN", email = (string?)null, phoneNumber = phone, password, confirmation = password });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "0977199991", password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(phone, loginBody.GetProperty("user").GetProperty("phoneNumber").GetString());
        client.DefaultRequestHeaders.Authorization = new("Bearer", loginBody.GetProperty("accessToken").GetString());
        var enroll = await client.PostAsJsonAsync("/api/v1/auth/pin/enroll", new { pin, confirmation = pin });
        Assert.Equal(HttpStatusCode.OK, enroll.StatusCode);
        await using (var db = Db())
        {
            var account = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.NormalizedPhoneNumber == phone);
            Assert.False(string.IsNullOrWhiteSpace(account.PinHash)); Assert.Equal(1, account.PinVersion); Assert.NotNull(account.PinEnrolledAtUtc); Assert.Equal(0, account.PinFailedAttemptCount); Assert.Null(account.PinLockedAtUtc);
        }
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer");
        foreach (var identifier in new[] { "0977199991", phone })
        {
            var unlock = await client.PostAsJsonAsync("/api/v1/auth/pin/unlock", new { phoneNumber = identifier, pin });
            Assert.Equal(HttpStatusCode.OK, unlock.StatusCode);
        }
    }

    [DockerFact]
    public async Task Public_role_registration_succeeds_without_birth_date_or_legal_business_name()
    {
        using var client = factory!.CreateClient(); const string password = "Registration-test-1!";
        var shopper = await client.PostAsJsonAsync("/api/v1/customers/register", new { displayName = "No DOB Shopper", email = (string?)null, phoneNumber = "0977000101", password, confirmation = password });
        Assert.Equal(HttpStatusCode.Created, shopper.StatusCode);
        var creator = await client.PostAsJsonAsync("/api/v1/creators/register", new { firstName = "No", lastName = "Birthdate", displayName = "No DOB Creator", phoneNumber = "0977000102", email = (string?)null, password, confirmation = password, preferredLanguage = "en", city = "Addis Ababa", biography = "", contentCategories = "Lifestyle", termsAccepted = true, socialProfiles = new[] { new { platform = "TikTok", handle = "no-dob-creator", profileUrl = (string?)null, followerCount = 10, isPrimary = true } } });
        Assert.Equal(HttpStatusCode.Created, creator.StatusCode);
        var merchant = await client.PostAsJsonAsync("/api/v1/merchants/register", new { tradingName = "No DOB Business", businessType = "Other", primaryContactName = "Business Owner", phoneNumber = "0977000103", email = (string?)null, password, confirmation = password, businessAddress = "Addis Ababa", city = "Addis Ababa", region = "Addis Ababa", country = "Ethiopia", timeZone = "Africa/Addis_Ababa", preferredLanguage = "en", termsAccepted = true, documents = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, merchant.StatusCode);
        await using var db = Db();
        Assert.Equal(3, await db.UserAccounts.CountAsync(x => new[] { "+251977000101", "+251977000102", "+251977000103" }.Contains(x.NormalizedPhoneNumber) && x.BirthDate == null));
        Assert.Equal("No DOB Business", await db.Merchants.Where(x => x.TradingName == "No DOB Business").Select(x => x.LegalBusinessName).SingleAsync());
    }

    [DockerFact]
    public async Task Reusable_offer_qr_checkout_validates_phone_and_posts_exactly_once()
    {
        using var client = factory!.CreateClient();
        var seedResponse = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seedResponse.StatusCode);
        var seed = await seedResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var location = seed.GetProperty("locationId").GetGuid();
        var payload = seed.GetProperty("offerQrPayload").GetString()!;
        var creatorCode = seed.GetProperty("creatorCode").GetString()!;
        var approvedCreator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var unapprovedCreator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000099", creatorId: "30000000-0000-0000-0000-000000000001", status: AccountStatus.PendingApproval);
        Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/v1/creator/campaigns/60000000-0000-0000-0000-000000000001/qr-image", approvedCreator)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/v1/creator/campaigns/60000000-0000-0000-0000-000000000001/qr-image", unapprovedCreator)).StatusCode);
        var permanentQr = await Get(client, "/api/v1/creator/qr/", approvedCreator);
        Assert.Equal(HttpStatusCode.OK, permanentQr.StatusCode);
        var permanentPayload = (await permanentQr.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("payload").GetString()!;
        await using (var db = Db())
        {
            var activeCampaign = await db.CreatorMerchantCampaigns.SingleAsync(x => x.Id == Guid.Parse("60000000-0000-0000-0000-000000000001"));
            var newerSuspended = new CreatorMerchantCampaign
            {
                Id = Guid.NewGuid(),
                PublicCampaignId = "CAM-SUSPENDED-NEWER",
                CreatorId = activeCampaign.CreatorId,
                MerchantId = activeCampaign.MerchantId,
                MerchantCreatorPartnershipId = activeCampaign.MerchantCreatorPartnershipId,
                CreatedAtUtc = DateTime.UtcNow
            };
            newerSuspended.Approve(365, null, activeCampaign.CommissionRuleVersionId, Guid.Parse("20000000-0000-0000-0000-000000000008"), "SUSPENDED-NEWER", null, DateTime.UtcNow);
            newerSuspended.Start(DateTime.UtcNow);
            newerSuspended.Suspend(DateTime.UtcNow);
            db.Add(newerSuspended);
            await db.SaveChangesAsync();
        }
        var permanentValidation = await Post(client, "/api/v1/cashier/checkouts/validate-offer", cashier, new { qrPayload = permanentPayload });
        Assert.Equal(HttpStatusCode.OK, permanentValidation.StatusCode);
        Assert.True((await permanentValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());
        var repeatedPermanentValidation = await Post(client, "/api/v1/cashier/checkouts/validate-offer", cashier, new { qrPayload = permanentPayload });
        Assert.Equal(HttpStatusCode.OK, repeatedPermanentValidation.StatusCode);
        Assert.True((await repeatedPermanentValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());

        var creatorValidation = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashier, new { creatorCode });
        Assert.Equal(HttpStatusCode.OK, creatorValidation.StatusCode);
        Assert.True((await creatorValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());
        var missingCreator = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashier, new { creatorCode = "9999" });
        Assert.Equal(HttpStatusCode.NotFound, missingCreator.StatusCode);
        Assert.Contains("Creator ID not found.", await missingCreator.Content.ReadAsStringAsync());
        var ineligibleCreator = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashier, new { creatorCode = seed.GetProperty("noCampaignCreatorCode").GetString() });
        Assert.Equal(HttpStatusCode.OK, ineligibleCreator.StatusCode);
        Assert.False((await ineligibleCreator.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());

        var unknown = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode, shopperPhoneNumber = "0911 999 999", purchaseAmount = 100m }, "unknown-shopper");
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal("shopper_not_registered", (await unknown.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());
        Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot());

        var first = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "direct-once");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var result = await first.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("awaiting_shopper_confirmation", result.GetProperty("code").GetString());
        Assert.Equal("09*****001", result.GetProperty("maskedPhoneNumber").GetString());
        Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot());
        var pendingId = result.GetProperty("checkout").GetProperty("id").GetGuid();
        Assert.True(result.GetProperty("checkout").GetProperty("expiresAtUtc").GetDateTime() > DateTime.UtcNow.AddHours(23));
        var confirmation = await Post(client, $"/api/v1/customer/checkouts/{pendingId}/approve", shopper, new { }, "shopper-confirms-direct-once");
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var afterFirst = await Snapshot();
        Assert.Equal((1, 1, 1, 1, 1, 1, 1), afterFirst);

        var duplicate = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode, shopperPhoneNumber = "+251911000001", purchaseAmount = 100m }, "direct-once");
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
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode); Assert.Contains("insufficient funds", await insufficient.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await using (var db = Db()) { var qr = await db.CampaignQrCodes.SingleAsync(x => x.PublicQrId == seed.GetProperty("offerQrId").GetString()); qr.Revoke(DateTime.UtcNow); await db.SaveChangesAsync(); }
        var disabled = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = payload, merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "disabled-offer");
        Assert.Equal(HttpStatusCode.Conflict, disabled.StatusCode);
    }

    [DockerFact]
    public async Task Cashier_without_location_assignment_can_validate_and_submit_new_purchase()
    {
        using var client = factory!.CreateClient();
        var seededResponse = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seededResponse.StatusCode);
        var seed = await seededResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var creatorCode = seed.GetProperty("creatorCode").GetString()!;
        var offerPayload = seed.GetProperty("offerQrPayload").GetString()!;

        await using (var db = Db())
        {
            var cashierId = Guid.Parse("20000000-0000-0000-0000-000000000005");
            db.CashierLocationAssignments.RemoveRange(db.CashierLocationAssignments.Where(x => x.CashierId == cashierId));
            await db.SaveChangesAsync();
        }

        var validation = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashier, new { creatorCode });
        Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
        Assert.True((await validation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());

        var locationlessOffer = await Post(client, "/api/v1/cashier/checkouts/validate-offer", cashier, new { qrPayload = offerPayload });
        Assert.Equal(HttpStatusCode.OK, locationlessOffer.StatusCode);
        var locationlessOfferBody = await locationlessOffer.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(locationlessOfferBody.GetProperty("isValid").GetBoolean());
        Assert.Equal("Eligible", locationlessOfferBody.GetProperty("code").GetString());

        var before = await Snapshot();
        var submitted = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "no-location-new-purchase");
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var body = await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("awaiting_shopper_confirmation", body.GetProperty("code").GetString());
        Assert.Equal(before, await Snapshot());

        var checkoutId = body.GetProperty("checkout").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{checkoutId}/approve", shopper, new { }, "no-location-new-purchase-confirm")).StatusCode);
        Assert.Equal((before.Purchases + 1, before.WalletEntries + 1, before.Earnings + 1, before.Cashback + 1, before.Snapshots + 1, before.Revenue + 1, before.Journals + 1), await Snapshot());
    }

    [DockerFact]
    public async Task Three_pending_confirmations_are_independent_and_resolve_to_one_completed_and_two_rejected()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var ids = new List<Guid>();
        foreach (var (amount, key) in new[] { (2000m, "multi-one"), (2100m, "multi-two"), (2200m, "multi-three") })
        {
            var response = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = amount }, key);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("awaiting_shopper_confirmation", body.GetProperty("code").GetString());
            ids.Add(body.GetProperty("checkout").GetProperty("id").GetGuid());
        }
        var pending = await (await Get(client, "/api/v1/customer/checkouts", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(3, pending.EnumerateArray().Count(x => x.GetProperty("status").GetString() == "AwaitingCustomerApproval"));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{ids[0]}/approve", shopper, new { }, "multi-yes")).StatusCode);
        var afterYes = await (await Get(client, "/api/v1/customer/checkouts", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2, afterYes.EnumerateArray().Count(x => x.GetProperty("status").GetString() == "AwaitingCustomerApproval"));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{ids[1]}/reject", shopper, new { })).StatusCode);
        var afterNo = await (await Get(client, "/api/v1/customer/checkouts", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Single(afterNo.EnumerateArray(), x => x.GetProperty("status").GetString() == "AwaitingCustomerApproval");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{ids[2]}/reject", shopper, new { })).StatusCode);
        var history = await (await Get(client, "/api/v1/customer/checkouts", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(history.EnumerateArray(), x => x.GetProperty("status").GetString() == "AwaitingCustomerApproval");
        Assert.Single(history.EnumerateArray(), x => x.GetProperty("status").GetString() == "Completed");
        Assert.Equal(2, history.EnumerateArray().Count(x => x.GetProperty("status").GetString() == "Rejected"));
        Assert.Equal((1, 1, 1, 1, 1, 1, 1), await Snapshot());
        await using var db = Db();
        Assert.Equal(3, await db.CheckoutSessions.CountAsync(x => ids.Contains(x.Id)));
        Assert.Equal(3, await db.Notifications.CountAsync(x => x.NotificationType == NotificationType.CheckoutApprovalRequired && ids.Select(id => id.ToString()).Contains(x.RelatedEntityId!)));
        Assert.Single(await db.PurchaseTransactions.ToListAsync());
        Assert.Single(await db.CreatorEarnings.ToListAsync());
        Assert.Single(await db.PlatformRevenueEntries.ToListAsync());
        Assert.Single(await db.FinancialJournals.Where(x => x.RelatedTransactionId != null).ToListAsync());
    }

    [DockerFact]
    public async Task Business_activation_creates_creator_id_promotion_and_deactivate_reactivate_controls_every_view()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var relationshipId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var creatorCode = seed.GetProperty("creatorCode").GetString()!;

        await using (var db = Db())
        {
            var relationship = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == relationshipId);
            var campaignIds = await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == relationshipId).Select(x => x.Id).ToArrayAsync();
            relationship.AssignedCampaignId = null;
            await db.SaveChangesAsync();
            await db.CheckoutSessions.Where(x => campaignIds.Contains(x.CampaignId)).ExecuteDeleteAsync();
            await db.SavedPromotions.Where(x => campaignIds.Contains(x.CampaignId)).ExecuteDeleteAsync();
            await db.CampaignCommissionAssignments.Where(x => x.MerchantCreatorPartnershipId == relationshipId).ExecuteDeleteAsync();
            await db.CampaignQrCodes.Where(x => x.Campaign.MerchantCreatorPartnershipId == relationshipId).ExecuteDeleteAsync();
            await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == relationshipId).ExecuteDeleteAsync();
        }

        var beforeActivation = await Get(client, "/api/v1/merchant/partnerships", owner);
        var beforeItems = await beforeActivation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(beforeItems.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == relationshipId).GetProperty("promotionActive").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/partnerships/{relationshipId}/activate", owner, new { reason = "Confirm advertising permission" })).StatusCode);
        const string videoUrl = "https://www.tiktok.com/@active-e2e-business/video/1234567890123456789";
        var promoVideo = await Post(client, $"/api/v1/creator/partnerships/{relationshipId}/promotion-video", creator, new { videoUrl });
        Assert.Equal(HttpStatusCode.Created, promoVideo.StatusCode);
        var promoVideoId = (await promoVideo.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/promotion-videos/{promoVideoId}/approve", owner, new { reason = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/creator/partnerships/{relationshipId}/go-live", creator, new { })).StatusCode);
        Guid campaignId;
        await using (var db = Db())
        {
            var campaign = await db.CreatorMerchantCampaigns.SingleAsync(x => x.MerchantCreatorPartnershipId == relationshipId);
            campaignId = campaign.Id;
            Assert.Equal(CampaignStatus.Active, campaign.Status);
            Assert.Null(await db.CampaignQrCodes.SingleOrDefaultAsync(x => x.CampaignId == campaign.Id));
        }

        var discovery = await Get(client, "/api/v1/customer/discovery/advertising", shopper);
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        var discoveryRows = await discovery.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var advertised = discoveryRows.EnumerateArray().Single(x => x.GetProperty("relationshipId").GetGuid() == relationshipId);
        Assert.Equal(creatorCode, advertised.GetProperty("creatorCode").GetString());
        Assert.Equal("Active", advertised.GetProperty("status").GetString());
        Assert.Equal(videoUrl, advertised.GetProperty("promotionVideoUrl").GetString());
        Assert.Equal("Live", advertised.GetProperty("promotionVideoStatus").GetString());
        Assert.True((await ValidateCreator(client, cashier, creatorCode)).GetProperty("isValid").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/partnerships/{relationshipId}/suspend", owner, new { reason = "Deactivate disposable promotion" })).StatusCode);
        Assert.False((await ValidateCreator(client, cashier, creatorCode)).GetProperty("isValid").GetBoolean());
        await using (var db = Db()) Assert.Equal(CampaignStatus.Suspended, (await db.CreatorMerchantCampaigns.SingleAsync(x => x.Id == campaignId)).Status);
        var afterDeactivationDiscovery = await (await Get(client, "/api/v1/customer/discovery/advertising", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(afterDeactivationDiscovery.EnumerateArray(), x => x.GetProperty("relationshipId").GetGuid() == relationshipId);
        var creatorItems = await (await Get(client, "/api/v1/creator/partnerships", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(creatorItems.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == relationshipId).GetProperty("promotionActive").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/partnerships/{relationshipId}/reactivate", owner, new { reason = "Reactivate disposable promotion" })).StatusCode);
        Assert.False((await ValidateCreator(client, cashier, creatorCode)).GetProperty("isValid").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/creator/partnerships/{relationshipId}/go-live", creator, new { })).StatusCode);
        Assert.True((await ValidateCreator(client, cashier, creatorCode)).GetProperty("isValid").GetBoolean());
        await using (var db = Db()) { var campaigns = await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == relationshipId).ToListAsync(); Assert.Single(campaigns); Assert.Equal(campaignId, campaigns[0].Id); Assert.Equal(CampaignStatus.Active, campaigns[0].Status); }

        var rejected = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "creator-id-reject");
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("awaiting_shopper_confirmation", rejectedBody.GetProperty("code").GetString());
        await Post(client, $"/api/v1/customer/checkouts/{rejectedBody.GetProperty("checkout").GetProperty("id").GetGuid()}/reject", shopper, new { });
        Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot());
        await using (var db = Db()) { var rejectedId = rejectedBody.GetProperty("checkout").GetProperty("id").GetGuid(); Assert.True(await db.NotificationRecipients.Where(x => x.Notification.RelatedEntityId == rejectedId.ToString() && x.Channel == NotificationChannel.InApp).AllAsync(x => x.ReadAtUtc != null)); }

        var accepted = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "creator-id-accept");
        var acceptedBody = await accepted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("awaiting_shopper_confirmation", acceptedBody.GetProperty("code").GetString());
        await using (var db = Db()) { var checkoutId = acceptedBody.GetProperty("checkout").GetProperty("id").GetGuid(); var recipient = await db.NotificationRecipients.Include(x => x.Notification).SingleAsync(x => x.Notification.RelatedEntityId == checkoutId.ToString()); Assert.Equal(NotificationChannel.InApp, recipient.Channel); Assert.Equal("Weymela", recipient.Notification.Title); Assert.Contains("Active E2E Business", recipient.Notification.Body); Assert.Contains("100.00 ETB", recipient.Notification.Body); Assert.Contains("Is this your purchase?", recipient.Notification.Body); Assert.False(recipient.ReadAtUtc.HasValue); Assert.False(await db.NotificationRecipients.AnyAsync(x => x.Notification.RelatedEntityId == checkoutId.ToString() && x.Channel == NotificationChannel.Push)); }
        await Approve(acceptedBody.GetProperty("checkout").GetProperty("id").GetGuid(), "creator-id-shopper-yes");
        Assert.Equal((1, 1, 1, 1, 1, 1, 1), await Snapshot());
        await using (var db = Db()) { var acceptedId = acceptedBody.GetProperty("checkout").GetProperty("id").GetGuid(); Assert.True(await db.NotificationRecipients.Where(x => x.Notification.RelatedEntityId == acceptedId.ToString() && x.Channel == NotificationChannel.InApp).AllAsync(x => x.ReadAtUtc != null)); }
        var businessMetrics = await (await Get(client, "/api/v1/merchant/dashboard-metrics", owner)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(1, businessMetrics.GetProperty("confirmedSales").GetInt32());
        var creatorSummary = await (await Get(client, "/api/v1/creator/earnings/summary", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(1, creatorSummary.GetProperty("currentPeriodConfirmedSales").GetInt32()); Assert.Equal(4m, creatorSummary.GetProperty("currentPayoutAmount").GetDecimal());
        await Approve(acceptedBody.GetProperty("checkout").GetProperty("id").GetGuid(), "creator-id-shopper-yes-repeat");
        Assert.Equal((1, 1, 1, 1, 1, 1, 1), await Snapshot());
    }

    [DockerFact]
    public async Task Activation_without_commission_configuration_is_safe_precise_and_atomic()
    {
        using var client = factory!.CreateClient(); var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001"); var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001"); var relationshipId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        await using (var db = Db()) { var campaignIds = await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == relationshipId).Select(x => x.Id).ToArrayAsync(); var relationship = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == relationshipId); relationship.AssignedCampaignId = null; await db.SaveChangesAsync(); await db.CheckoutSessions.Where(x => campaignIds.Contains(x.CampaignId)).ExecuteDeleteAsync(); await db.SavedPromotions.Where(x => campaignIds.Contains(x.CampaignId)).ExecuteDeleteAsync(); await db.CampaignCommissionAssignments.Where(x => x.MerchantCreatorPartnershipId == relationshipId).ExecuteDeleteAsync(); await db.CampaignQrCodes.Where(x => campaignIds.Contains(x.CampaignId)).ExecuteDeleteAsync(); await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == relationshipId).ExecuteDeleteAsync(); await db.PlatformCommissionAssignments.ExecuteDeleteAsync(); }
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, $"/api/v1/merchant/partnerships/{relationshipId}/activate", creator, new { reason = "Creator may not activate" })).StatusCode);
        var permission = await Post(client, $"/api/v1/merchant/partnerships/{relationshipId}/activate", owner, new { reason = "Owner permission approval" }); Assert.Equal(HttpStatusCode.OK, permission.StatusCode);
        var response = await Post(client, $"/api/v1/creator/partnerships/{relationshipId}/go-live", creator, new { }); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Contains("Platform commission settings are configured", await response.Content.ReadAsStringAsync());
        await using var verify = Db(); Assert.Empty(await verify.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == relationshipId).ToListAsync()); Assert.Equal(PartnershipStatus.Approved, (await verify.MerchantCreatorPartnerships.SingleAsync(x => x.Id == relationshipId)).Status);
    }

    [DockerFact]
    public async Task Multiple_same_day_purchases_post_financials_once_per_checkout()
    {
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);

        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");

        var first = await CreateAndPresent(client, shopper, cashier, "first");
        await Approve(first, "approve-first");

        var duplicate = await CreateAndPresent(client, shopper, cashier, "duplicate");
        await Approve(duplicate, "approve-duplicate");

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
            Assert.Equal(8m, await db.CreatorBalanceAccounts.Where(x => x.CreatorId == Guid.Parse("30000000-0000-0000-0000-000000000001")).Select(x => x.PendingBalance).SingleAsync());
            Assert.Equal(6m, await db.CustomerWallets.Where(x => x.CustomerId == Guid.Parse("10000000-0000-0000-0000-000000000001")).Select(x => x.AvailableCashback).SingleAsync());
        }

        var third = await CreateAndPresent(client, shopper, cashier, "third");
        await Approve(third, "approve-third");
        await using (var db = Db())
        {
            Assert.Equal(3, await db.PurchaseTransactions.CountAsync());
            Assert.Equal(3, await db.MerchantWalletEntries.CountAsync(x => x.EntryType == MerchantWalletEntryType.CommissionDebit));
            Assert.Equal(3, await db.CreatorEarnings.CountAsync());
        }
    }

    [DockerFact]
    public async Task Fresh_yes_purchases_settle_at_small_medium_and_large_amounts()
    {
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        await using (var db = Db())
        {
            var campaign = await db.CreatorMerchantCampaigns.SingleAsync(x => x.Id == Guid.Parse("60000000-0000-0000-0000-000000000001"));
            typeof(CreatorMerchantCampaign).GetProperty(nameof(CreatorMerchantCampaign.ReuseRule))!.SetValue(campaign, OfferReuseRule.Unlimited);
            var wallet = await db.MerchantWallets.SingleAsync(x => x.MerchantId == Guid.Parse("20000000-0000-0000-0000-000000000001"));
            wallet.Credit(10_000m, 0m, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        foreach (var amount in new[] { 300m, 2_000m, 3_500m })
        {
            var id = await CreateAndPresent(client, shopper, cashier, $"amount-{amount}", amount);
            await Approve(id, $"approve-{amount}");
        }

        await using var verify = Db();
        Assert.Equal(3, await verify.PurchaseTransactions.CountAsync(x => x.Status == TransactionStatus.Confirmed));
        Assert.Equal(3, await verify.CreatorEarnings.CountAsync());
        Assert.Equal(3, await verify.CustomerCashbackEntries.CountAsync(x => x.EntryType == CustomerCashbackEntryType.Earned));
        Assert.Equal(3, await verify.PlatformRevenueEntries.CountAsync());
        Assert.Equal(3, await verify.FinancialJournals.CountAsync(x => x.IsPosted));
        Assert.Equal(12, await verify.FinancialJournalLines.CountAsync());
        Assert.Equal(232m, await verify.CreatorEarnings.SumAsync(x => x.Amount));
        Assert.Equal(174m, await verify.CustomerCashbackEntries.Where(x => x.EntryType == CustomerCashbackEntryType.Earned).SumAsync(x => x.Amount));
        Assert.Equal(174m, await verify.PlatformRevenueEntries.SumAsync(x => x.Amount));
        Assert.Equal(10_420m, await verify.MerchantWallets.Where(x => x.MerchantId == Guid.Parse("20000000-0000-0000-0000-000000000001")).Select(x => x.AvailableBalance).SingleAsync());
    }

    [DockerFact]
    public async Task Large_cashier_purchases_continue_when_commission_is_fully_funded()
    {
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        await using (var db = Db())
        {
            var wallet = await db.MerchantWallets.SingleAsync(x => x.MerchantId == Guid.Parse("20000000-0000-0000-0000-000000000001"));
            wallet.Credit(10_000m, 0m, DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");

        var first = await CreateAndPresent(client, shopper, cashier, "large-12000", 12_000m);
        await Approve(first, "large-12000-confirm");
        var second = await CreateAndPresent(client, shopper, cashier, "large-20000", 20_000m);
        await Approve(second, "large-20000-confirm");

        await using var verify = Db();
        Assert.Equal(2, await verify.PurchaseTransactions.CountAsync(x => x.Status == TransactionStatus.Confirmed));
        Assert.Equal(2, await verify.CreatorEarnings.CountAsync());
        Assert.Equal(2, await verify.CustomerCashbackEntries.CountAsync(x => x.EntryType == CustomerCashbackEntryType.Earned));
        Assert.Equal(2, await verify.PlatformRevenueEntries.CountAsync());
        Assert.Equal(2, await verify.FinancialJournals.CountAsync(x => x.IsPosted));
        Assert.Equal(8, await verify.FinancialJournalLines.CountAsync());
        Assert.Equal(7_800m, await verify.MerchantWallets.Where(x => x.MerchantId == Guid.Parse("20000000-0000-0000-0000-000000000001")).Select(x => x.AvailableBalance).SingleAsync());
        Assert.Equal(1_280m, await verify.CreatorEarnings.SumAsync(x => x.Amount));
        Assert.Equal(960m, await verify.CustomerCashbackEntries.Where(x => x.EntryType == CustomerCashbackEntryType.Earned).SumAsync(x => x.Amount));
        Assert.Equal(960m, await verify.PlatformRevenueEntries.SumAsync(x => x.Amount));
        Assert.Equal(new[] { 12_000m, 20_000m }, await verify.PurchaseTransactions.OrderBy(x => x.PurchaseAmount).Select(x => x.PurchaseAmount).ToListAsync());
    }

    [DockerFact]
    public async Task Purchase_amount_must_be_positive_before_checkout_is_created()
    {
        using var client = factory!.CreateClient();
        var seedResponse = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seedResponse.StatusCode);
        var seed = await seedResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var response = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode = seed!.GetProperty("creatorCode").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = 0m }, "zero-amount");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Purchase amount must be positive", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task Deposit_proof_stays_pending_until_admin_review_and_approval_posts_once()
    {
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");

        var otherOwner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000099", merchantId: "20000000-0000-0000-0000-000000000002");
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var before = await WalletBalance();

        var submitted = await Deposit(client, owner, "proof-deposit-1", 500m, "proof.png", "image/png", Png());
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var body = await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var id = body.GetProperty("id").GetGuid();
        Assert.Equal("PendingVerification", body.GetProperty("status").GetString());
        var secondSubmitted = await Deposit(client, owner, "proof-deposit-2", 200m, "proof.jpg", "image/jpeg", Jpeg());
        Assert.Equal(HttpStatusCode.Created, secondSubmitted.StatusCode);
        var rejectedId = (await secondSubmitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.NotEqual(id, rejectedId);
        var ownerDeposits = await (await Get(client, "/api/v1/merchant/deposits", owner)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2, ownerDeposits.GetArrayLength());
        Assert.All(ownerDeposits.EnumerateArray(), x => Assert.Equal("PendingVerification", x.GetProperty("status").GetString()));
        var adminDeposits = await (await Get(client, "/api/v1/admin/deposits", admin)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(adminDeposits.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Contains(adminDeposits.EnumerateArray(), x => x.GetProperty("id").GetGuid() == rejectedId);
        Assert.Equal(before, await WalletBalance());
        Assert.Equal(HttpStatusCode.OK, (await Get(client, $"/api/v1/admin/deposits/{id}/proof", admin)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(client, $"/api/v1/merchant/deposits/{id}/proof", otherOwner)).StatusCode);

        var approved = await Post(client, $"/api/v1/admin/deposits/{id}/approve", admin, new { }, "approve-proof-1");
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(before + 500m, await WalletBalance());
        ownerDeposits = await (await Get(client, "/api/v1/merchant/deposits", owner)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("PendingVerification", ownerDeposits.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == rejectedId).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/deposits/{id}/approve", admin, new { }, "approve-proof-duplicate")).StatusCode);
        Assert.Equal(before + 500m, await WalletBalance());
        await using (var db = Db())
        {
            Assert.Single(await db.MerchantWalletEntries.Where(x => x.RelatedDepositId == id).ToListAsync());
            Assert.Single(await db.FinancialJournals.Where(x => x.RelatedDepositId == id && x.IsPosted).ToListAsync());
            Assert.True(await db.MerchantAuditEvents.AnyAsync(x => x.EventType == "DepositApproved" && x.Detail != null && x.Detail.Contains(id.ToString())));
        }

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/deposits/{rejectedId}/reject", admin, new { reason = "Receipt could not be verified" })).StatusCode);
        Assert.Equal(before + 500m, await WalletBalance());
        Assert.Equal(HttpStatusCode.BadRequest, (await Deposit(client, owner, "proof-deposit-invalid", 100m, "proof.exe", "application/octet-stream", [1, 2, 3])).StatusCode);
    }

    [DockerFact]
    public async Task Creator_can_deactivate_active_relationship_without_removing_qr_or_history()
    {
        using var client = factory!.CreateClient(); var seedResponse = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, seedResponse.StatusCode); var seed = await seedResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var qr = await Get(client, "/api/v1/creator/qr/", creator); Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/creator/partnerships/40000000-0000-0000-0000-000000000001/stop-promoting", creator, new { })).StatusCode);
        await AssertDeactivated();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var checkout = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), merchantLocationId = seed.GetProperty("locationId").GetGuid(), shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "deactivated-checkout");
        Assert.Equal(HttpStatusCode.Conflict, checkout.StatusCode); Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/api/v1/creator/partnerships/40000000-0000-0000-0000-000000000001/stop-promoting", creator, new { })).StatusCode);
    }

    [DockerFact]
    public async Task Business_can_deactivate_active_relationship()
    {
        using var client = factory!.CreateClient(); var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, seeded.StatusCode); var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001"); Assert.Equal(HttpStatusCode.OK, (await Get(client, "/api/v1/creator/qr/", creator)).StatusCode);
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var sale = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), merchantLocationId = seed.GetProperty("locationId").GetGuid(), shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "deactivation-history-sale");
        var saleBody = await sale.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal("awaiting_shopper_confirmation", saleBody.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{saleBody.GetProperty("checkout").GetProperty("id").GetGuid()}/approve", shopper, new { }, "confirm-deactivation-history-sale")).StatusCode);
        var activePerformance = await (await Get(client, "/api/v1/creator/ads/performance", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var activeBusiness = activePerformance.GetProperty("businesses").EnumerateArray().Single(x => x.GetProperty("partnershipId").GetGuid() == Guid.Parse("40000000-0000-0000-0000-000000000001"));
        Assert.Equal("Active", activeBusiness.GetProperty("status").GetString()); Assert.Equal(1, activeBusiness.GetProperty("confirmedSales").GetInt32()); Assert.Equal(4m, activeBusiness.GetProperty("creatorEarned").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/merchant/partnerships/40000000-0000-0000-0000-000000000001/revoke", owner, new { reason = "Pilot deactivation" })).StatusCode);
        await AssertDeactivated(expectFinancialHistory: true);
        var deactivatedPerformance = await (await Get(client, "/api/v1/creator/ads/performance", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var deactivatedBusiness = deactivatedPerformance.GetProperty("businesses").EnumerateArray().Single(x => x.GetProperty("partnershipId").GetGuid() == Guid.Parse("40000000-0000-0000-0000-000000000001"));
        Assert.Equal("Deactivated", deactivatedBusiness.GetProperty("status").GetString()); Assert.Equal(1, deactivatedBusiness.GetProperty("confirmedSales").GetInt32()); Assert.Equal(4m, deactivatedBusiness.GetProperty("creatorEarned").GetDecimal()); Assert.Single(deactivatedPerformance.GetProperty("transactions").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), merchantLocationId = seed.GetProperty("locationId").GetGuid(), shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "blocked-after-deactivation")).StatusCode);
        await using var history = Db(); Assert.Single(await history.CreatorEarnings.ToListAsync()); Assert.Equal(4m, await history.CreatorEarnings.SumAsync(x => x.Amount));
    }

    [DockerFact]
    public async Task Confirmed_sales_are_transaction_backed_private_and_separate_from_payouts()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var otherCreator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000022", creatorId: "30000000-0000-0000-0000-000000000021");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var otherOwner = Token(UserRole.MerchantAdmin, "21000000-0000-0000-0000-000000000012", merchantId: "21000000-0000-0000-0000-000000000002");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var firstShopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var secondShopper = Token(UserRole.Customer, "11000000-0000-0000-0000-000000000011", customerId: "11000000-0000-0000-0000-000000000001");

        var before = await (await Get(client, "/api/v1/creator/ads/performance", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Empty(before.GetProperty("transactions").EnumerateArray());
        Assert.Contains(before.GetProperty("businesses").EnumerateArray(), x => x.GetProperty("status").GetString() == "Active" && x.GetProperty("confirmedSales").GetInt32() == 0);

        async Task<Guid> StartSale(string shopperPhone, string key)
        {
            var response = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), merchantLocationId = seed.GetProperty("locationId").GetGuid(), shopperPhoneNumber = shopperPhone, purchaseAmount = 100m }, key);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal("awaiting_shopper_confirmation", body.GetProperty("code").GetString());
            return body.GetProperty("checkout").GetProperty("id").GetGuid();
        }

        var firstCheckout = await StartSale("0911000001", "confirmed-sales-one");
        var unconfirmedCreator = await (await Get(client, "/api/v1/creator/ads/performance", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var unconfirmedMerchant = await (await Get(client, "/api/v1/merchant/confirmed-sales", owner)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Empty(unconfirmedCreator.GetProperty("transactions").EnumerateArray());
        Assert.Equal(0, unconfirmedMerchant.GetProperty("confirmedSales").GetInt32());

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{firstCheckout}/approve", firstShopper, new { }, "confirmed-sales-one-approve")).StatusCode);
        var secondCheckout = await StartSale("0922000001", "confirmed-sales-two");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{secondCheckout}/approve", secondShopper, new { }, "confirmed-sales-two-approve")).StatusCode);

        var creatorReport = await (await Get(client, "/api/v1/creator/ads/performance", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var creatorSales = creatorReport.GetProperty("transactions").EnumerateArray().ToList();
        Assert.Equal(2, creatorSales.Count);
        Assert.All(creatorSales, sale =>
        {
            Assert.Equal("Confirmed", sale.GetProperty("status").GetString());
            Assert.Equal(4m, sale.GetProperty("creatorEarned").GetDecimal());
            Assert.False(sale.TryGetProperty("purchaseAmount", out _));
            Assert.False(sale.TryGetProperty("saleAmount", out _));
            Assert.False(sale.TryGetProperty("customer", out _));
            Assert.False(sale.TryGetProperty("shopper", out _));
        });
        var otherCreatorReport = await (await Get(client, "/api/v1/creator/ads/performance", otherCreator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Empty(otherCreatorReport.GetProperty("transactions").EnumerateArray());

        var merchantReport = await (await Get(client, "/api/v1/merchant/confirmed-sales", owner)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2, merchantReport.GetProperty("confirmedSales").GetInt32());
        Assert.Equal(200m, merchantReport.GetProperty("totalSalesAmount").GetDecimal());
        Assert.Equal(20m, merchantReport.GetProperty("totalCommissionAmount").GetDecimal());
        Assert.Equal(2, merchantReport.GetProperty("sales").GetArrayLength());
        var otherMerchantReport = await (await Get(client, "/api/v1/merchant/confirmed-sales", otherOwner)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, otherMerchantReport.GetProperty("confirmedSales").GetInt32());

        var payoutSummary = await (await Get(client, "/api/v1/creator/earnings/summary", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(2, payoutSummary.GetProperty("currentPeriodConfirmedSales").GetInt32());
        Assert.Equal(8m, payoutSummary.GetProperty("currentPayoutAmount").GetDecimal());
        var payouts = await (await Get(client, "/api/v1/creator/payouts", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Empty(payouts.EnumerateArray());
    }

    [DockerFact]
    public async Task Business_creator_discovery_returns_only_active_approved_accounts_and_supports_name_public_id_and_handle()
    {
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");

        await using (var db = Db())
        {
            var now = DateTime.UtcNow;
            var locked = await db.Creators.SingleAsync(x => x.DisplayName == "Desktop Invite Creator");
            var ranked = await db.Creators.SingleAsync(x => x.DisplayName == "Mobile Invite Creator");
            (await db.CreatorSocialProfiles.SingleAsync(x => x.CreatorId == locked.Id && x.IsPrimary)).FollowerCount = 250_000;
            (await db.CreatorSocialProfiles.SingleAsync(x => x.CreatorId == ranked.Id && x.IsPrimary)).FollowerCount = 125_000;
            (await db.UserAccounts.SingleAsync(x => x.CreatorId == locked.Id && x.Role == UserRole.Creator)).LockoutEndUtc = now.AddHours(1);
            await db.SaveChangesAsync();
        }

        foreach (var query in new[] { "Selam", "CRE-0000000000000001", "weymela-e2e" })
        {
            var response = await Get(client, $"/api/v1/merchant/creators/search?q={Uri.EscapeDataString(query)}", owner);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var results = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.DoesNotContain(results.EnumerateArray(), x => x.GetProperty("displayName").GetString() == "Selam Active");
        }

        var all = await Get(client, "/api/v1/merchant/creators/search?q=", owner);
        var creators = await all.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(creators.EnumerateArray(), x => x.GetProperty("displayName").GetString()!.Contains("Pending Creator"));
        Assert.DoesNotContain(creators.EnumerateArray(), x => x.GetProperty("displayName").GetString() == "Suspended Creator");

        var top = await Get(client, "/api/v1/merchant/creators/search?q=&sort=followers", owner);
        Assert.Equal(HttpStatusCode.OK, top.StatusCode);
        var rankedCreators = (await top.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).EnumerateArray().ToList();
        Assert.DoesNotContain(rankedCreators, x => x.GetProperty("displayName").GetString() == "Desktop Invite Creator");
        Assert.Equal("Mobile Invite Creator", rankedCreators[0].GetProperty("displayName").GetString());
        Assert.Equal(125_000, rankedCreators[0].GetProperty("followerCount").GetInt64());
    }

    [DockerFact]
    public async Task Creator_business_discovery_lists_active_business_with_existing_relationship_and_blocks_inactive_accounts()
    {
        using var client = factory!.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var response = await Get(client, "/api/v1/creator/merchants/search?q=", creator); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var businesses = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(businesses.EnumerateArray(), x => x.GetProperty("tradingName").GetString() == "Active E2E Business");
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/api/v1/creator/partnerships/requests", creator, new { merchantId = Guid.Parse("20000000-0000-0000-0000-000000000001"), introductoryMessage = (string?)null })).StatusCode);
        await using var db = Db(); var owner = await db.UserAccounts.SingleAsync(x => x.MerchantId == Guid.Parse("20000000-0000-0000-0000-000000000001") && x.Role == UserRole.MerchantAdmin); owner.Status = AccountStatus.Suspended; await db.SaveChangesAsync();
        response = await Get(client, "/api/v1/creator/merchants/search?q=Active", creator); businesses = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Empty(businesses.EnumerateArray());
    }

    [DockerFact]
    public async Task Shopper_business_discovery_filters_relationships_and_selected_creator_uses_single_use_checkout_token()
    {
        using var client = factory!.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var list = await Get(client, "/api/v1/customer/discovery/businesses?q=Addis", shopper); Assert.Equal(HttpStatusCode.OK, list.StatusCode); var businesses = await list.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var business = Assert.Single(businesses.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Active E2E Business"); Assert.True(business.GetProperty("rewardsAvailable").GetBoolean());
        Assert.DoesNotContain(businesses.EnumerateArray(), x => x.GetProperty("businessName").GetString() == "Desktop Workflow Business");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        await using (var promoDb = Db())
        {
            var activePartnerships = await promoDb.MerchantCreatorPartnerships.Where(x => x.MerchantId == Guid.Parse("20000000-0000-0000-0000-000000000001") && x.Status == PartnershipStatus.Approved).Select(x => new { x.Id, x.CreatorId }).ToListAsync();
            var videoSequence = 0;
            foreach (var partnership in activePartnerships)
            {
                videoSequence++;
                var creatorUser = await promoDb.UserAccounts.SingleAsync(x => x.CreatorId == partnership.CreatorId && x.Role == UserRole.Creator);
                var creatorToken = Token(UserRole.Creator, creatorUser.Id.ToString(), creatorId: partnership.CreatorId.ToString());
                var videoUrl = $"https://www.tiktok.com/@e2e/video/900000000000000{videoSequence:D4}";
                var submitted = await Post(client, $"/api/v1/creator/partnerships/{partnership.Id}/promotion-video", creatorToken, new { videoUrl }, $"promo-{partnership.Id:N}");
                Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
                var promoVideoId = (await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
                Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/promotion-videos/{promoVideoId}/approve", owner, new { reason = (string?)null }, $"approve-{partnership.Id:N}")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/creator/partnerships/{partnership.Id}/go-live", creatorToken, new { }, $"go-live-{partnership.Id:N}")).StatusCode);
            }
        }
        var advertisingResponse = await Get(client, "/api/v1/customer/discovery/advertising?q=Addis", shopper); Assert.Equal(HttpStatusCode.OK, advertisingResponse.StatusCode); var advertising = await advertisingResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(2, advertising.GetArrayLength()); Assert.All(advertising.EnumerateArray(), x => Assert.Equal("Active E2E Business", x.GetProperty("businessName").GetString()));
        var mimiRows = await (await Get(client, "/api/v1/customer/discovery/advertising?q=Mimi", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var mimiRow = Assert.Single(mimiRows.EnumerateArray()); Assert.Equal("Mimi Active", mimiRow.GetProperty("creatorName").GetString());
        var qrImage = await Get(client, $"/api/v1/customer/discovery/advertising/{mimiRow.GetProperty("relationshipId").GetGuid()}/qr-image", shopper); Assert.Equal(HttpStatusCode.OK, qrImage.StatusCode); Assert.Equal("image/png", qrImage.Content.Headers.ContentType?.MediaType); Assert.True((await qrImage.Content.ReadAsByteArrayAsync()).Length > 100);
        var profile = await Get(client, "/api/v1/customer/profile", shopper); Assert.Equal(HttpStatusCode.OK, profile.StatusCode); var profileBody = await profile.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal("E2E Shopper", profileBody.GetProperty("name").GetString()); Assert.True(profileBody.GetProperty("isEmailVerified").GetBoolean());
        var id = business.GetProperty("businessId").GetGuid(); var detail = await Get(client, $"/api/v1/customer/discovery/businesses/{id}", shopper); var body = await detail.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(2, body.GetProperty("creators").GetArrayLength());
        var chosen = body.GetProperty("creators").EnumerateArray().Single(x => x.GetProperty("displayName").GetString() == "Mimi Active");
        var create = await Post(client, "/api/v1/customer/checkouts", shopper, new { campaignId = chosen.GetProperty("campaignId").GetGuid() }, "shopper-selects-mimi"); Assert.Equal(HttpStatusCode.Created, create.StatusCode); var checkout = await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(chosen.GetProperty("creatorId").GetGuid(), checkout.GetProperty("creatorId").GetGuid());
        var payload = checkout.GetProperty("qrPayload").GetString(); var presented = await Post(client, "/api/v1/cashier/checkouts/present", cashier, new { qrPayload = payload, merchantLocationId = Guid.Parse("20000000-0000-0000-0000-000000000003"), purchaseAmount = 100m }, "present-selected-creator"); Assert.Equal(HttpStatusCode.OK, presented.StatusCode);
        var duplicate = await Post(client, "/api/v1/cashier/checkouts/present", cashier, new { qrPayload = payload, merchantLocationId = Guid.Parse("20000000-0000-0000-0000-000000000003"), purchaseAmount = 100m }, "present-selected-creator-again"); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        await using (var db = Db()) { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE merchant_creator_partnerships SET \"EndDateUtc\" = {DateTime.UtcNow.AddSeconds(-1)} WHERE \"Id\" = {Guid.Parse("40000000-0000-0000-0000-000000000002")}"); }
        body = await (await Get(client, $"/api/v1/customer/discovery/businesses/{id}", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Single(body.GetProperty("creators").EnumerateArray());
        advertising = await (await Get(client, "/api/v1/customer/discovery/advertising", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Single(advertising.EnumerateArray()); Assert.DoesNotContain(advertising.EnumerateArray(), x => x.GetProperty("creatorName").GetString() == "Mimi Active");
        await using (var db = Db()) { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE merchant_creator_partnerships SET \"EndDateUtc\" = {DateTime.UtcNow.AddDays(20)} WHERE \"Id\" = {Guid.Parse("40000000-0000-0000-0000-000000000002")}"); var second = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == Guid.Parse("40000000-0000-0000-0000-000000000002")); second.Revoke(DateTime.UtcNow, Guid.Parse("10000000-0000-0000-0000-000000000002")); await db.SaveChangesAsync(); }
        body = await (await Get(client, $"/api/v1/customer/discovery/businesses/{id}", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Single(body.GetProperty("creators").EnumerateArray());
        advertising = await (await Get(client, "/api/v1/customer/discovery/advertising", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Single(advertising.EnumerateArray()); Assert.DoesNotContain(advertising.EnumerateArray(), x => x.GetProperty("creatorName").GetString() == "Mimi Active");
        await SetBusinessTypeMinimumAsync("Other", 1000m);
        await using (var db = Db())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 2000 WHERE \"MerchantId\" = '20000000-0000-0000-0000-000000000001'");
            await db.Database.ExecuteSqlRawAsync("UPDATE merchant_locations SET \"IsActive\" = FALSE WHERE \"MerchantId\" = '20000000-0000-0000-0000-000000000001'");
            await db.SaveChangesAsync();
        }
        // Shopper visibility is based on the active promotion, not on an unrelated merchant-location requirement.
        advertising = await (await Get(client, "/api/v1/customer/discovery/advertising", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.NotEmpty(advertising.EnumerateArray());
        await using (var db = Db()) { await db.Database.ExecuteSqlRawAsync("UPDATE merchant_locations SET \"IsActive\" = TRUE WHERE \"MerchantId\" = '20000000-0000-0000-0000-000000000001'; UPDATE \"MerchantWallets\" SET \"AvailableBalance\" = 900 WHERE \"MerchantId\" = '20000000-0000-0000-0000-000000000001'"); }
        var hiddenDetail = await Get(client, $"/api/v1/customer/discovery/businesses/{id}", shopper);
        Assert.Equal(HttpStatusCode.NotFound, hiddenDetail.StatusCode);
        var hiddenDetailBody = await hiddenDetail.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Business is unavailable.", hiddenDetailBody.GetProperty("detail").GetString());
        advertising = await (await Get(client, "/api/v1/customer/discovery/advertising", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.DoesNotContain(advertising.EnumerateArray(), x => x.GetProperty("businessId").GetGuid() == id);
        await SetBusinessTypeMinimumAsync("Other", 0m);
    }

    [DockerFact]
    public async Task Business_activation_deactivation_and_reactivation_keep_all_views_and_cashier_eligibility_consistent()
    {
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var creatorId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var creatorUserId = Guid.Parse("30000000-0000-0000-0000-000000000026");
        var relationshipId = Guid.Parse("40000000-0000-0000-0000-000000000026");
        await using (var db = Db())
        {
            var creator = await db.Creators.SingleAsync(x => x.Id == creatorId);
            creator.PhoneNumber = creator.NormalizedPhoneNumber = "+251944000026";
            db.UserAccounts.Add(new UserAccount { Id = creatorUserId, Email = "activation-creator@e2e.invalid", NormalizedEmail = "ACTIVATION-CREATOR@E2E.INVALID", PhoneNumber = creator.PhoneNumber, NormalizedPhoneNumber = creator.NormalizedPhoneNumber, Role = UserRole.Creator, Status = AccountStatus.Active, CreatorId = creatorId, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = DateTime.UtcNow });
            var relationship = new MerchantCreatorPartnership { Id = relationshipId, MerchantId = Guid.Parse("20000000-0000-0000-0000-000000000001"), CreatorId = creatorId, RequestedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow };
            relationship.Approve(DateTime.UtcNow, Guid.Parse("20000000-0000-0000-0000-000000000008"));
            db.MerchantCreatorPartnerships.Add(relationship);
            await db.SaveChangesAsync();
        }

        var before = await Get(client, "/api/v1/merchant/partnerships", owner);
        var beforeRows = await before.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(beforeRows.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == relationshipId).GetProperty("promotionActive").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/partnerships/{relationshipId}/activate", owner, new { reason = "Confirm advertising permission" })).StatusCode);
        var creatorToken = Token(UserRole.Creator, creatorUserId.ToString(), creatorId: creatorId.ToString());
        const string activationVideoUrl = "https://www.tiktok.com/@activation-e2e/video/1234567890123456791";
        var promoVideo = await Post(client, $"/api/v1/creator/partnerships/{relationshipId}/promotion-video", creatorToken, new { videoUrl = activationVideoUrl }, "activation-promo");
        Assert.Equal(HttpStatusCode.Created, promoVideo.StatusCode);
        var promoVideoId = (await promoVideo.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/promotion-videos/{promoVideoId}/approve", owner, new { reason = (string?)null }, "activation-promo-approve")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/creator/partnerships/{relationshipId}/go-live", creatorToken, new { }, "activation-promo-go-live")).StatusCode);
        await AssertPromotionState(client, owner, shopper, cashier, relationshipId, "4828", true);
        var submitted = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode = "4828", shopperPhoneNumber = "+251911000001", purchaseAmount = 100m }, "creator-id-lifecycle-submit");
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal("AwaitingShopperConfirmation", (await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/partnerships/{relationshipId}/suspend", owner, new { reason = "Business deactivated ad" })).StatusCode);
        await AssertPromotionState(client, owner, shopper, cashier, relationshipId, "4828", false);

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/partnerships/{relationshipId}/reactivate", owner, new { reason = "Business reactivated ad" })).StatusCode);
        await AssertPromotionState(client, owner, shopper, cashier, relationshipId, "4828", false);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/creator/partnerships/{relationshipId}/go-live", creatorToken, new { }, "activation-promo-go-live-again")).StatusCode);
        await AssertPromotionState(client, owner, shopper, cashier, relationshipId, "4828", true);

        await using (var db = Db()) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CreatorMerchantCampaigns\" SET \"ExpiresAtUtc\" = {DateTime.UtcNow.AddSeconds(-1)} WHERE \"MerchantCreatorPartnershipId\" = {relationshipId}");
        var expiredValidation = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashier, new { creatorCode = "4828" });
        Assert.Equal(HttpStatusCode.OK, expiredValidation.StatusCode); Assert.False((await expiredValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());

        await using (var db = Db())
        {
            var activeCampaign = await db.CreatorMerchantCampaigns.SingleAsync(x => x.MerchantCreatorPartnershipId == Guid.Parse("40000000-0000-0000-0000-000000000001") && x.Status == CampaignStatus.Active);
            var wallet = await db.MerchantWallets.SingleAsync(x => x.MerchantId == activeCampaign.MerchantId);
            await SetBusinessTypeMinimumAsync("Other", wallet.AvailableBalance + 1m);
        }
        var activeRows = await (await Get(client, "/api/v1/merchant/partnerships", owner)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(activeRows.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == Guid.Parse("40000000-0000-0000-0000-000000000001")).GetProperty("promotionActive").GetBoolean());
        var fundingValidation = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashier, new { creatorCode = "4827" });
        Assert.Equal(HttpStatusCode.OK, fundingValidation.StatusCode); Assert.True((await fundingValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());
    }

    private static async Task AssertPromotionState(HttpClient client, string owner, string shopper, string cashier, Guid relationshipId, string creatorCode, bool active)
    {
        var ownerRows = await (await Get(client, "/api/v1/merchant/partnerships", owner)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(active, ownerRows.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == relationshipId).GetProperty("promotionActive").GetBoolean());
        var shopperRows = await (await Get(client, "/api/v1/customer/discovery/advertising", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(active, shopperRows.EnumerateArray().Any(x => x.GetProperty("relationshipId").GetGuid() == relationshipId && x.GetProperty("creatorCode").GetString() == creatorCode));
        var validation = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashier, new { creatorCode });
        Assert.Equal(HttpStatusCode.OK, validation.StatusCode);
        Assert.Equal(active, (await validation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());
    }

    [DockerFact]
    public async Task Platform_admin_can_bootstrap_missing_financial_configuration_without_changing_history()
    {
        using var client = factory!.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        await using (var db = Db()) { db.PlatformCommissionAssignments.RemoveRange(db.PlatformCommissionAssignments); await db.SaveChangesAsync(); }
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001"); var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var loaded = await Get(client, "/api/v1/admin/financial-settings", admin); Assert.Equal(HttpStatusCode.OK, loaded.StatusCode); var settings = await loaded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(10m, settings.GetProperty("merchantCommissionRatePercent").GetDecimal()); Assert.Equal(9, settings.GetProperty("businessTypeMinimumWalletBalances").GetArrayLength()); Assert.All(settings.GetProperty("businessTypeMinimumWalletBalances").EnumerateArray(), x => Assert.Equal(0m, x.GetProperty("minimumBusinessWalletBalance").GetDecimal()));
        var valid = FinancialSettingsRequest(12m, 45m, 25m, 30m, 2000m); Assert.Equal(HttpStatusCode.Forbidden, (await Put(client, "/api/v1/admin/financial-settings", owner, valid)).StatusCode);
        var invalid = FinancialSettingsRequest(12m, 45m, 25m, 20m, 2000m); Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, "/api/v1/admin/financial-settings", admin, invalid)).StatusCode);
        var applied = await Put(client, "/api/v1/admin/financial-settings", admin, valid); Assert.Equal(HttpStatusCode.OK, applied.StatusCode); var appliedBody = await applied.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.True((DateTime.UtcNow - appliedBody!.GetProperty("effectiveAtUtc").GetDateTime()).Duration() < TimeSpan.FromSeconds(5));
        await using var verify = Db(); var platform = await verify.PlatformCommissionAssignments.SingleAsync(); Assert.Equal(12m, (await verify.CommissionRuleVersions.SingleAsync(x => x.CommissionRuleId == platform.CommissionRuleId)).MerchantCommissionRatePercent); Assert.Equal(2000m, await verify.BusinessTypeWalletMinimumVersions.Where(x => x.BusinessType == "Other").OrderByDescending(x => x.VersionNumber).Select(x => x.MinimumBusinessWalletBalance).FirstAsync()); var payoutSchedule = await verify.PayoutScheduleVersions.SingleAsync(); Assert.Equal(1, payoutSchedule.VersionNumber); Assert.Equal(DayOfWeek.Friday, payoutSchedule.CreatorCutoffDay); Assert.Equal(DayOfWeek.Saturday, payoutSchedule.CreatorPayoutDay); Assert.Empty(await verify.CommissionCalculationSnapshots.ToListAsync()); Assert.Contains(await verify.CommissionAuditEvents.ToListAsync(), x => x.EventType == "FinancialSettingsChanged");
    }

    [DockerFact]
    public async Task Future_payout_schedule_is_versioned_and_does_not_change_current_or_historical_payouts()
    {
        using var client = factory!.CreateClient(); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001"); var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var before = await (await Get(client, "/api/v1/creator/earnings/summary", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var beforeDate = before.GetProperty("nextEstimatedPayoutAtUtc").GetDateTime(); var effective = DateTime.UtcNow.AddDays(2);
        var request = new { merchantCommissionRatePercent = 10m, creatorSharePercent = 40m, shopperSharePercent = 30m, platformSharePercent = 30m, businessTypeMinimumWalletBalances = BusinessTypes.Values.Select(type => new { businessType = type, minimumBusinessWalletBalance = 0m }).ToArray(), minimumTikTokFollowers = 0, applyNow = false, effectiveFromUtc = effective, creatorCutoffDay = "Sunday", creatorCutoffTime = "03:30:00", creatorPayoutDay = "Monday", shopperCutoffDay = 20, shopperCutoffTime = "04:00:00", shopperPayoutDay = 21 };
        var rejectedPast = new { merchantCommissionRatePercent = 10m, creatorSharePercent = 40m, shopperSharePercent = 30m, platformSharePercent = 30m, businessTypeMinimumWalletBalances = BusinessTypes.Values.Select(type => new { businessType = type, minimumBusinessWalletBalance = 0m }).ToArray(), minimumTikTokFollowers = 0, applyNow = false, effectiveFromUtc = DateTime.UtcNow.AddMinutes(-5), creatorCutoffDay = "Sunday", creatorCutoffTime = "03:30:00", creatorPayoutDay = "Monday", shopperCutoffDay = 20, shopperCutoffTime = "04:00:00", shopperPayoutDay = 21 };
        Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, "/api/v1/admin/financial-settings", admin, rejectedPast)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Put(client, "/api/v1/admin/financial-settings", admin, request)).StatusCode);
        var unchanged = await (await Get(client, "/api/v1/creator/earnings/summary", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(beforeDate, unchanged.GetProperty("nextEstimatedPayoutAtUtc").GetDateTime());
        using var scope = factory.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<ICreatorEarningsService>(); var future = await service.GetSummaryAsync(Guid.Parse("30000000-0000-0000-0000-000000000001"), effective.AddMinutes(1), CancellationToken.None); Assert.Equal(DayOfWeek.Monday, future.NextEstimatedPayoutAtUtc!.Value.DayOfWeek);
        await using var db = Db(); var version = await db.PayoutScheduleVersions.SingleAsync(); Assert.InRange((version.EffectiveFromUtc - effective).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1)); Assert.Equal(DayOfWeek.Monday, version.CreatorPayoutDay); Assert.Empty(await db.CreatorPayouts.ToListAsync()); Assert.Empty(await db.CustomerPayoutRequests.ToListAsync());
    }

    [DockerFact]
    public async Task Creator_weekly_payout_moves_current_to_reserved_then_paid_without_reusing_earnings()
    {
        using var client = factory!.CreateClient(); var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, seeded.StatusCode); var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var submitted = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = 1500m }, "weekly-payout-sale"); var checkout = (await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("checkout").GetProperty("id").GetGuid(); Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{checkout}/approve", shopper, new { }, "weekly-payout-confirm")).StatusCode);
        var current = await (await Get(client, "/api/v1/creator/earnings/summary", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(60m, current.GetProperty("currentPayoutAmount").GetDecimal()); Assert.Equal(60m, current.GetProperty("upcomingPayoutAmount").GetDecimal()); Assert.Equal(1, current.GetProperty("currentPeriodConfirmedSales").GetInt32()); Assert.Equal(1, current.GetProperty("confirmedSalesCount").GetInt32());
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001"); var sales = await Get(client, "/api/v1/merchant/confirmed-sales", owner); Assert.Equal(HttpStatusCode.OK, sales.StatusCode); var salesBody = await sales.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(1, salesBody.GetProperty("confirmedSales").GetInt32()); Assert.Equal(1500m, salesBody.GetProperty("totalSalesAmount").GetDecimal()); Assert.Single(salesBody.GetProperty("sales").EnumerateArray());
        var cycle = await Get(client, "/api/v1/admin/payout-cycles/creators", admin); Assert.Equal(HttpStatusCode.OK, cycle.StatusCode); var cycleBody = await cycle.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(60m, cycleBody.GetProperty("scheduledTotal").GetDecimal()); var accumulatingLine = Assert.Single(cycleBody.GetProperty("lines").EnumerateArray()); Assert.Equal("Selam Active", accumulatingLine.GetProperty("partyName").GetString()); Assert.Equal("4827", accumulatingLine.GetProperty("partyId").GetString()); Assert.Equal("UNPAID", accumulatingLine.GetProperty("status").GetString()); Assert.Equal(0m, accumulatingLine.GetProperty("reservedAmount").GetDecimal()); Assert.Equal(JsonValueKind.Null, accumulatingLine.GetProperty("payoutId").ValueKind);
        var cutoff = DateTime.UtcNow.AddSeconds(1); var batchResponse = await Post(client, "/api/v1/admin/payout-batches", admin, new { cutoffAtUtc = cutoff, scheduledForUtc = cutoff.AddDays(1), currencyCode = "ETB", idempotencyKey = "weekly-cycle-1" }); Assert.Equal(HttpStatusCode.Created, batchResponse.StatusCode); var batch = await batchResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var batchId = batch.GetProperty("id").GetGuid(); var payoutId = batch.GetProperty("payouts")[0].GetProperty("id").GetGuid();
        var reserved = await (await Get(client, "/api/v1/creator/earnings/summary", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(0m, reserved.GetProperty("currentPayoutAmount").GetDecimal()); Assert.Equal(60m, reserved.GetProperty("upcomingPayoutAmount").GetDecimal()); Assert.Equal(60m, reserved.GetProperty("scheduledBalance").GetDecimal()); var reservedCycle = await (await Get(client, "/api/v1/admin/payout-cycles/creators", admin)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var unpaidCreator = Assert.Single(reservedCycle.GetProperty("lines").EnumerateArray()); Assert.Equal("UNPAID", unpaidCreator.GetProperty("status").GetString()); Assert.Equal(60m, unpaidCreator.GetProperty("reservedAmount").GetDecimal()); Assert.Equal(payoutId, unpaidCreator.GetProperty("payoutId").GetGuid()); Assert.Equal(batchId, unpaidCreator.GetProperty("payoutBatchId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/payout-batches/{batchId}/process", admin, new { })).StatusCode); Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/payouts/{payoutId}/submit", admin, new { providerReference = "manual-weekly-1" })).StatusCode); Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/payouts/{payoutId}/mark-paid", admin, new { })).StatusCode); Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/payouts/{payoutId}/mark-paid", admin, new { })).StatusCode);
        var paid = await (await Get(client, "/api/v1/creator/earnings/summary", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(0m, paid.GetProperty("currentPayoutAmount").GetDecimal()); Assert.Equal(0m, paid.GetProperty("upcomingPayoutAmount").GetDecimal()); Assert.Equal(0, paid.GetProperty("currentPeriodConfirmedSales").GetInt32()); Assert.Equal(1, paid.GetProperty("confirmedSalesCount").GetInt32()); Assert.Equal(0m, paid.GetProperty("scheduledBalance").GetDecimal()); Assert.True(paid.GetProperty("lastPayoutAtUtc").GetDateTime() > DateTime.UtcNow.AddMinutes(-1)); var paidCycle = await (await Get(client, "/api/v1/admin/payout-cycles/creators", admin)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var paidCreator = Assert.Single(paidCycle.GetProperty("lines").EnumerateArray()); Assert.Equal("PAID", paidCreator.GetProperty("status").GetString()); Assert.Equal(0m, paidCreator.GetProperty("reservedAmount").GetDecimal()); Assert.NotEmpty(paidCycle.GetProperty("history").EnumerateArray());
        var secondSale = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = 1000m }, "weekly-payout-sale-2"); var secondCheckout = (await secondSale.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("checkout").GetProperty("id").GetGuid(); Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{secondCheckout}/approve", shopper, new { }, "weekly-payout-confirm-2")).StatusCode);
        var next = await (await Get(client, "/api/v1/creator/earnings/summary", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(40m, next.GetProperty("upcomingPayoutAmount").GetDecimal()); Assert.Equal(2, next.GetProperty("confirmedSalesCount").GetInt32()); var payoutHistory = await (await Get(client, "/api/v1/creator/payouts", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var completed = Assert.Single(payoutHistory.EnumerateArray()); Assert.Equal(60m, completed.GetProperty("amount").GetDecimal()); Assert.Equal("Paid", completed.GetProperty("status").GetString());
        await using var db = Db(); Assert.Equal(2, await db.CreatorEarnings.CountAsync()); Assert.Equal(CreatorEarningStatus.Paid, (await db.CreatorEarnings.OrderBy(x => x.EarnedAtUtc).FirstAsync()).Status); Assert.Single(await db.PayoutItems.ToListAsync()); Assert.Equal(2, await db.PurchaseTransactions.CountAsync());
    }

    [DockerFact]
    public async Task Shopper_monthly_payout_resets_current_values_and_preserves_cashback_history()
    {
        using var client = factory!.CreateClient(); var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, seeded.StatusCode); var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var submitted = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "monthly-payout-sale"); var checkout = (await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("checkout").GetProperty("id").GetGuid(); Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{checkout}/approve", shopper, new { }, "monthly-payout-confirm")).StatusCode);
        var before = await (await Get(client, "/api/v1/customer/wallet", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(3m, before.GetProperty("availableCashback").GetDecimal()); Assert.Equal(1, before.GetProperty("currentPeriodConfirmedPurchases").GetInt32()); Assert.Equal(DateTimeKind.Utc, before.GetProperty("nextPayoutAtUtc").GetDateTime().Kind); var shopperCycle = await Get(client, "/api/v1/admin/payout-cycles/shoppers", admin); Assert.Equal(HttpStatusCode.OK, shopperCycle.StatusCode); var shopperCycleBody = await shopperCycle.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(3m, shopperCycleBody.GetProperty("scheduledTotal").GetDecimal()); var accumulatingShopper = Assert.Single(shopperCycleBody.GetProperty("lines").EnumerateArray()); Assert.Equal("E2E Shopper", accumulatingShopper.GetProperty("partyName").GetString()); Assert.Equal("CUS-E2E", accumulatingShopper.GetProperty("partyId").GetString()); Assert.Equal("UNPAID", accumulatingShopper.GetProperty("status").GetString()); Assert.Equal(0m, accumulatingShopper.GetProperty("reservedAmount").GetDecimal()); Assert.Equal(JsonValueKind.Null, accumulatingShopper.GetProperty("payoutId").ValueKind); var revenue = await Get(client, "/api/v1/admin/payout-cycles/platform-revenue", admin); Assert.Equal(HttpStatusCode.OK, revenue.StatusCode); var revenueBody = await revenue.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(3m, revenueBody.GetProperty("currentRevenue").GetDecimal()); var revenueHistory = Assert.Single(revenueBody.GetProperty("history").EnumerateArray()); Assert.Equal(1, revenueHistory.GetProperty("transactionCount").GetInt32()); Assert.Equal(3m, revenueHistory.GetProperty("platformRevenue").GetDecimal());
        var requested = await Post(client, "/api/v1/customer/cashback-payouts", shopper, new { }, "monthly-cycle-1"); Assert.Equal(HttpStatusCode.Created, requested.StatusCode); var payoutId = (await requested.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid(); var reserved = await (await Get(client, "/api/v1/customer/wallet", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(0m, reserved.GetProperty("availableCashback").GetDecimal()); Assert.Equal(3m, reserved.GetProperty("reservedCashback").GetDecimal()); var reservedShopperCycle = await (await Get(client, "/api/v1/admin/payout-cycles/shoppers", admin)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var unpaidShopper = Assert.Single(reservedShopperCycle.GetProperty("lines").EnumerateArray()); Assert.Equal("UNPAID", unpaidShopper.GetProperty("status").GetString()); Assert.Equal(0m, unpaidShopper.GetProperty("eligibleAmount").GetDecimal()); Assert.Equal(3m, unpaidShopper.GetProperty("reservedAmount").GetDecimal()); Assert.Equal(payoutId, unpaidShopper.GetProperty("payoutId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/customer-payouts/{payoutId}/processing", admin, new { })).StatusCode); var paidAt = DateTime.UtcNow; Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/customer-payouts/{payoutId}/mark-paid", admin, new { externalMethod = "Manual", externalReference = "monthly-1", paidAtUtc = paidAt, safeNote = "Disposable test" }, "monthly-paid-1")).StatusCode);
        var after = await (await Get(client, "/api/v1/customer/wallet", shopper)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal(0m, after.GetProperty("availableCashback").GetDecimal()); Assert.Equal(0m, after.GetProperty("reservedCashback").GetDecimal()); Assert.Equal(0, after.GetProperty("currentPeriodConfirmedPurchases").GetInt32()); Assert.True(after.GetProperty("lastPayoutAtUtc").GetDateTime() > DateTime.UtcNow.AddMinutes(-1)); Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/customer-payouts/{payoutId}/mark-paid", admin, new { externalMethod = "Manual", externalReference = "monthly-1", paidAtUtc = paidAt, safeNote = "Repeat" }, "monthly-paid-1")).StatusCode); var paidShopperCycle = await (await Get(client, "/api/v1/admin/payout-cycles/shoppers", admin)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var paidShopper = Assert.Single(paidShopperCycle.GetProperty("lines").EnumerateArray()); Assert.Equal("PAID", paidShopper.GetProperty("status").GetString()); Assert.Equal(0m, paidShopper.GetProperty("reservedAmount").GetDecimal()); Assert.NotEmpty(paidShopperCycle.GetProperty("history").EnumerateArray());
        await using var db = Db(); Assert.Single(await db.PurchaseTransactions.ToListAsync()); Assert.Single(await db.CustomerPayoutRequests.ToListAsync()); Assert.Equal(3, await db.CustomerCashbackEntries.CountAsync()); Assert.Equal(3m, (await db.CustomerWallets.SingleAsync(x => x.CustomerId == Guid.Parse("10000000-0000-0000-0000-000000000001"))).PaidLifetime);
    }

    [DockerFact]
    public async Task Admin_assisted_password_reset_is_private_audited_and_revokes_sessions()
    {
        using var client = factory!.CreateClient(); var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        await using (var setup = Db()) { _ = await setup.UserAccounts.SingleAsync(x => x.NormalizedPhoneNumber == "+251911000001"); }
        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "0911000001", password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode); var oldRefresh = (await oldLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString();
        var mismatch = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = "0911000001" });
        Assert.Equal(HttpStatusCode.OK, mismatch.StatusCode);
        var mismatchBody = await mismatch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", mismatchBody!.GetProperty("status").GetString());
        Assert.Equal("Password reset request submitted successfully. Your request is waiting for admin approval.", mismatchBody.GetProperty("message").GetString());
        var submitted = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = "0911000001" });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var submittedBody = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", submittedBody!.GetProperty("status").GetString());
        Assert.Equal("A password reset request is already pending admin approval.", submittedBody.GetProperty("message").GetString());
        var reference = submittedBody.GetProperty("reference").GetString()!;
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001"); var queue = await (await Get(client, "/api/v1/admin/password-reset-requests", admin)).Content.ReadFromJsonAsync<JsonElement>(); var item = Assert.Single(queue.EnumerateArray()); Assert.Equal("Pending", item.GetProperty("status").GetString()); Assert.False(item.TryGetProperty("birthDate", out _));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/password-reset-requests/{item.GetProperty("id").GetGuid()}/approve", admin, new { })).StatusCode); Assert.Equal(HttpStatusCode.Conflict, (await Post(client, $"/api/v1/admin/password-reset-requests/{item.GetProperty("id").GetGuid()}/approve", admin, new { })).StatusCode);
        Assert.Equal("Approved", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/password-reset-requests/{reference}")).GetProperty("status").GetString());
        const string replacement = "Replacement-password-2!"; Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { resetToken = reference, newPassword = replacement, confirmation = replacement })).StatusCode); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { resetToken = reference, newPassword = "Another-password-3!", confirmation = "Another-password-3!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "0911000001", password = "E2e-test-password-1!" })).StatusCode); Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "0911000001", password = replacement })).StatusCode); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = oldRefresh })).StatusCode);
        await using var verify = Db(); Assert.False(await verify.SupportRequests.AnyAsync(x => x.PublicReference == reference)); Assert.Single(await verify.PasswordResetTokens.Where(x => x.UserAccountId == Guid.Parse("10000000-0000-0000-0000-000000000002")).ToListAsync()); Assert.Contains(await verify.LoginAudits.ToListAsync(), x => x.FailureReason == "PasswordResetHelpRequested"); Assert.Contains(await verify.OperationalAuditEvents.ToListAsync(), x => x.EventType == "PasswordResetAuthorized" && x.MetadataJson.Contains(reference)); Assert.Contains(await verify.OperationalAuditEvents.ToListAsync(), x => x.EventType == "PasswordResetCompleted" && x.MetadataJson.Contains(reference));
    }

    [DockerFact]
    public async Task Password_reset_requests_publish_admin_notifications_and_surface_pending_approved_and_rejected_states()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var seed = await seeded.Content.ReadFromJsonAsync<JsonElement>();
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var shopperPhone = seed.GetProperty("shopperPhone").GetString()!;
        var creatorPhone = seed.GetProperty("creatorPhone").GetString()!;
        var ownerPhone = seed.GetProperty("ownerPhone").GetString()!;
        const string originalPassword = "E2e-test-password-1!";
        const string replacementPassword = "Replacement-password-2!";

        var summary0 = await (await Get(client, "/api/v1/admin/dashboard/summary", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, summary0!.GetProperty("openSupportRequests").GetInt32());
        var unread0 = await (await Get(client, "/api/v1/notifications/unread-count", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, unread0!.GetProperty("count").GetInt32());

        var customerReset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = shopperPhone });
        Assert.Equal(HttpStatusCode.OK, customerReset.StatusCode);
        var customerBody = await customerReset.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", customerBody!.GetProperty("status").GetString());
        Assert.Equal("Password reset request submitted successfully. Your request is waiting for admin approval.", customerBody.GetProperty("message").GetString());
        var customerReference = customerBody.GetProperty("reference").GetString()!;
        var customerRepeat = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = shopperPhone });
        Assert.Equal(HttpStatusCode.OK, customerRepeat.StatusCode);
        var customerRepeatBody = await customerRepeat.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(customerReference, customerRepeatBody!.GetProperty("reference").GetString());
        Assert.Equal("Pending", customerRepeatBody.GetProperty("status").GetString());

        var summary1 = await (await Get(client, "/api/v1/admin/dashboard/summary", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, summary1!.GetProperty("openSupportRequests").GetInt32());
        var unread1 = await (await Get(client, "/api/v1/notifications/unread-count", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, unread1!.GetProperty("count").GetInt32());
        var inbox1 = await (await Get(client, "/api/v1/notifications?page=1&pageSize=10", admin)).Content.ReadFromJsonAsync<JsonElement>();
        var customerNotice = Assert.Single(inbox1!.GetProperty("items").EnumerateArray(), x => x.GetProperty("type").GetString() == "SupportRequestReceived" && x.GetProperty("data").GetProperty("SupportReference").GetString() == customerReference);
        Assert.Equal("/admin/password-reset-requests#password-reset-requests", customerNotice.GetProperty("data").GetProperty("TargetPath").GetString());
        Assert.Equal("Customer", customerNotice.GetProperty("data").GetProperty("RequesterRole").GetString());
        Assert.Contains("Password reset request pending for", customerNotice.GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Contains("waiting for admin approval", customerNotice.GetProperty("body").GetString(), StringComparison.Ordinal);
        var customerNoticeJson = customerNotice.GetRawText();
        Assert.DoesNotContain(originalPassword, customerNoticeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("resetToken", customerNoticeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("newPassword", customerNoticeJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirmation", customerNoticeJson, StringComparison.OrdinalIgnoreCase);

        var queue = await (await Get(client, "/api/v1/admin/password-reset-requests", admin)).Content.ReadFromJsonAsync<JsonElement>();
        var customerItem = Assert.Single(queue!.EnumerateArray(), x => x.GetProperty("phone").GetString() == shopperPhone);
        Assert.Equal("Pending", customerItem.GetProperty("status").GetString());
        Assert.True(customerItem.GetProperty("canApprove").GetBoolean());
        Assert.True(customerItem.GetProperty("canDelete").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/password-reset-requests/{customerItem.GetProperty("id").GetGuid()}/approve", admin, new { })).StatusCode);
        var approvedStatus = await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/password-reset-requests/{customerReference}");
        Assert.Equal("Approved", approvedStatus!.GetProperty("status").GetString());
        Assert.Equal("Your request was approved. Create a new password.", approvedStatus.GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { resetToken = customerReference, newPassword = replacementPassword, confirmation = replacementPassword })).StatusCode);
        var completedStatus = await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/password-reset-requests/{customerReference}");
        Assert.Equal("Completed", completedStatus!.GetProperty("status").GetString());
        Assert.Equal("Password reset completed.", completedStatus.GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = shopperPhone, password = originalPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = shopperPhone, password = replacementPassword })).StatusCode);
        queue = await (await Get(client, "/api/v1/admin/password-reset-requests", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(queue!.EnumerateArray(), x => x.GetProperty("phone").GetString() == shopperPhone);

        var creatorReset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = creatorPhone });
        Assert.Equal(HttpStatusCode.OK, creatorReset.StatusCode);
        var creatorBody = await creatorReset.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", creatorBody!.GetProperty("status").GetString());
        var creatorReference = creatorBody.GetProperty("reference").GetString()!;
        queue = await (await Get(client, "/api/v1/admin/password-reset-requests", admin)).Content.ReadFromJsonAsync<JsonElement>();
        var creatorItem = Assert.Single(queue!.EnumerateArray(), x => x.GetProperty("phone").GetString() == creatorPhone);
        Assert.Equal("Pending", creatorItem.GetProperty("status").GetString());
        Assert.True(creatorItem.GetProperty("canApprove").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/password-reset-requests/{creatorItem.GetProperty("id").GetGuid()}/reject", admin, new { })).StatusCode);
        var rejectedStatus = await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/password-reset-requests/{creatorReference}");
        Assert.Equal("Rejected", rejectedStatus!.GetProperty("status").GetString());
        Assert.Equal("Your password reset request was rejected. Please contact Weymela Support or submit a new request.", rejectedStatus.GetProperty("message").GetString());

        var creatorRetry = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = creatorPhone });
        Assert.Equal(HttpStatusCode.OK, creatorRetry.StatusCode);
        var creatorRetryBody = await creatorRetry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", creatorRetryBody!.GetProperty("status").GetString());
        Assert.NotEqual(creatorReference, creatorRetryBody.GetProperty("reference").GetString());
        Assert.Equal("Password reset request submitted successfully. Your request is waiting for admin approval.", creatorRetryBody.GetProperty("message").GetString());
        queue = await (await Get(client, "/api/v1/admin/password-reset-requests", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(queue!.EnumerateArray(), x => x.GetProperty("phone").GetString() == creatorPhone && x.GetProperty("status").GetString() == "Pending");
        Assert.DoesNotContain(queue.EnumerateArray(), x => x.GetProperty("phone").GetString() == creatorPhone && x.GetProperty("status").GetString() == "Rejected");

        var ownerReset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = ownerPhone });
        Assert.Equal(HttpStatusCode.OK, ownerReset.StatusCode);
        var ownerBody = await ownerReset.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", ownerBody!.GetProperty("status").GetString());
        var ownerReference = ownerBody.GetProperty("reference").GetString()!;
        queue = await (await Get(client, "/api/v1/admin/password-reset-requests", admin)).Content.ReadFromJsonAsync<JsonElement>();
        var ownerItem = Assert.Single(queue!.EnumerateArray(), x => x.GetProperty("phone").GetString() == ownerPhone);
        Assert.Equal("Pending", ownerItem.GetProperty("status").GetString());
        Assert.True(ownerItem.GetProperty("canApprove").GetBoolean());
        Assert.All(queue.EnumerateArray(), x => Assert.Equal("Pending", x.GetProperty("status").GetString()));
        summary1 = await (await Get(client, "/api/v1/admin/dashboard/summary", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, summary1!.GetProperty("openSupportRequests").GetInt32());
        unread1 = await (await Get(client, "/api/v1/notifications/unread-count", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, unread1!.GetProperty("count").GetInt32());
        inbox1 = await (await Get(client, "/api/v1/notifications?page=1&pageSize=10", admin)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, inbox1!.GetProperty("items").GetArrayLength());
        Assert.Contains(inbox1.GetProperty("items").EnumerateArray(), x => x.GetProperty("data").GetProperty("SupportReference").GetString() == ownerReference);
    }

    [DockerFact]
    public async Task Password_reset_help_for_unknown_email_or_phone_returns_not_found_without_creating_requests()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        await using var beforeDb = Db();
        var before = await beforeDb.SupportRequests.CountAsync(x => x.Subject == "Password Reset");

        var missingPhone = "0911777888";
        var missingEmail = "missing-reset@e2e.invalid";

        var phoneResponse = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = missingPhone });
        Assert.Equal(HttpStatusCode.NotFound, phoneResponse.StatusCode);
        Assert.Contains("No account was found with that phone number or email. Please create a new account.", await phoneResponse.Content.ReadAsStringAsync());

        var emailResponse = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { email = missingEmail });
        Assert.Equal(HttpStatusCode.NotFound, emailResponse.StatusCode);
        Assert.Contains("No account was found with that phone number or email. Please create a new account.", await emailResponse.Content.ReadAsStringAsync());

        await using var afterDb = Db();
        Assert.Equal(before, await afterDb.SupportRequests.CountAsync(x => x.Subject == "Password Reset"));
        Assert.Equal(2, await afterDb.LoginAudits.CountAsync(x => x.FailureReason == "PasswordResetHelpNoAccount"));
        Assert.DoesNotContain(await afterDb.SupportRequests.Where(x => x.Subject == "Password Reset").Select(x => x.Contact).ToListAsync(), x => x == missingPhone || x == missingEmail);
    }

    [DockerFact]
    public async Task Password_reset_request_delete_is_available_to_platform_and_operations_admins_without_deleting_user_accounts()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var seed = await seeded.Content.ReadFromJsonAsync<JsonElement>();
        var platformAdmin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var creatorToken = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");

        var opsCreate = await Post(client, "/api/v1/admin/accounts/create", platformAdmin, new
        {
            role = "OperationsAdmin",
            email = "ops-reset-delete@e2e.invalid",
            phoneNumber = "0911000887",
            password = "E2e-test-password-1!",
            confirmation = "E2e-test-password-1!"
        });
        Assert.Equal(HttpStatusCode.Created, opsCreate.StatusCode);
        var opsLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ops-reset-delete@e2e.invalid", password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, opsLogin.StatusCode);
        var opsToken = (await opsLogin.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("accessToken").GetString()!;

        var customerReset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = seed.GetProperty("shopperPhone").GetString() });
        Assert.Equal(HttpStatusCode.OK, customerReset.StatusCode);
        var customerBody = await customerReset.Content.ReadFromJsonAsync<JsonElement>();
        var customerReference = customerBody!.GetProperty("reference").GetString()!;
        var customerQueue = await (await Get(client, "/api/v1/admin/password-reset-requests", platformAdmin)).Content.ReadFromJsonAsync<JsonElement>();
        var customerItem = Assert.Single(customerQueue!.EnumerateArray(), x => x.GetProperty("phone").GetString() == seed.GetProperty("shopperPhone").GetString());
        Assert.True(customerItem.GetProperty("canDelete").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/password-reset-requests/{customerItem.GetProperty("id").GetGuid()}/approve", opsToken, new { })).StatusCode);
        Assert.Equal("Approved", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/password-reset-requests/{customerReference}")).GetProperty("status").GetString());

        var creatorReset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = seed.GetProperty("creatorPhone").GetString() });
        Assert.Equal(HttpStatusCode.OK, creatorReset.StatusCode);
        var creatorBody = await creatorReset.Content.ReadFromJsonAsync<JsonElement>();
        var creatorReference = creatorBody!.GetProperty("reference").GetString()!;
        var creatorQueue = await (await Get(client, "/api/v1/admin/password-reset-requests", platformAdmin)).Content.ReadFromJsonAsync<JsonElement>();
        var creatorItem = Assert.Single(creatorQueue!.EnumerateArray(), x => x.GetProperty("phone").GetString() == seed.GetProperty("creatorPhone").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/admin/password-reset-requests/{creatorItem.GetProperty("id").GetGuid()}/reject", opsToken, new { })).StatusCode);
        Assert.Equal("Rejected", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/auth/password-reset-requests/{creatorReference}")).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/password-reset-requests/{creatorItem.GetProperty("id").GetGuid()}/delete", opsToken, new { reason = "Remove stale rejected request" })).StatusCode);

        var ownerReset = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = seed.GetProperty("ownerPhone").GetString() });
        Assert.Equal(HttpStatusCode.OK, ownerReset.StatusCode);
        var ownerBody = await ownerReset.Content.ReadFromJsonAsync<JsonElement>();
        var ownerReference = ownerBody!.GetProperty("reference").GetString()!;
        var ownerQueue = await (await Get(client, "/api/v1/admin/password-reset-requests", platformAdmin)).Content.ReadFromJsonAsync<JsonElement>();
        var ownerItem = Assert.Single(ownerQueue!.EnumerateArray(), x => x.GetProperty("phone").GetString() == seed.GetProperty("ownerPhone").GetString());
        var creatorAttempt = await Post(client, $"/api/v1/admin/password-reset-requests/{ownerItem.GetProperty("id").GetGuid()}/delete", creatorToken, new { reason = "Creators cannot delete reset requests" });
        Assert.Equal(HttpStatusCode.Forbidden, creatorAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/password-reset-requests/{ownerItem.GetProperty("id").GetGuid()}/delete", platformAdmin, new { reason = "Cancel stale pending request" })).StatusCode);

        await using var db = Db();
        Assert.False(await db.SupportRequests.AnyAsync(x => x.PublicReference == creatorReference));
        Assert.False(await db.SupportRequests.AnyAsync(x => x.PublicReference == ownerReference));
        Assert.True(await db.UserAccounts.AnyAsync(x => x.Email == seed.GetProperty("ownerEmail").GetString()));
        Assert.True(await db.UserAccounts.AnyAsync(x => x.Email == seed.GetProperty("creatorEmail").GetString()));

        var freshRetry = await client.PostAsJsonAsync("/api/v1/auth/password-reset-requests", new { phoneNumber = seed.GetProperty("ownerPhone").GetString() });
        Assert.Equal(HttpStatusCode.OK, freshRetry.StatusCode);
        var freshRetryBody = await freshRetry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", freshRetryBody!.GetProperty("status").GetString());
    }

    [DockerFact]
    public async Task Platform_admin_contact_visibility_and_persistent_funding_threshold_are_enforced()
    {
        using var client = factory!.CreateClient(); var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, seeded.StatusCode); var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var accounts = await Get(client, "/api/v1/admin/accounts?page=1&pageSize=100", admin); Assert.Equal(HttpStatusCode.OK, accounts.StatusCode); var accountPage = await accounts.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var ownerRow = Assert.Single(accountPage.GetProperty("items").EnumerateArray(), x => x.GetProperty("email").GetString() == "owner@e2e.invalid"); Assert.True(ownerRow.TryGetProperty("merchantId", out var merchantId)); Assert.Equal(JsonValueKind.String, merchantId.ValueKind); Assert.False(string.IsNullOrWhiteSpace(merchantId.GetString())); Assert.Contains(accountPage.GetProperty("items").EnumerateArray(), x => x.GetProperty("email").GetString() == "owner@e2e.invalid" && !string.IsNullOrWhiteSpace(x.GetProperty("phone").GetString()));
        var filtered = await (await Get(client, "/api/v1/admin/accounts?page=1&pageSize=25&role=Cashier&business=Active%20E2E%20Business&q=0911", admin)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.All(filtered.GetProperty("items").EnumerateArray(), x => { Assert.Equal("Cashier", x.GetProperty("role").GetString()); Assert.Equal("Active E2E Business", x.GetProperty("businessName").GetString()); }); Assert.NotEmpty(filtered.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/v1/admin/accounts", owner)).StatusCode);

        var settings = await Get(client, "/api/v1/admin/financial-settings", admin); Assert.Equal(HttpStatusCode.OK, settings.StatusCode); var current = await settings.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var raised = FinancialSettingsRequest(current.GetProperty("merchantCommissionRatePercent").GetDecimal(), current.GetProperty("creatorSharePercent").GetDecimal(), current.GetProperty("shopperSharePercent").GetDecimal(), current.GetProperty("platformSharePercent").GetDecimal(), 1000.01m);
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(client, "/api/v1/admin/financial-settings", owner, raised)).StatusCode);
        var negative = FinancialSettingsRequest(current.GetProperty("merchantCommissionRatePercent").GetDecimal(), current.GetProperty("creatorSharePercent").GetDecimal(), current.GetProperty("shopperSharePercent").GetDecimal(), current.GetProperty("platformSharePercent").GetDecimal(), -1m); Assert.Equal(HttpStatusCode.BadRequest, (await Put(client, "/api/v1/admin/financial-settings", admin, negative)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Put(client, "/api/v1/admin/financial-settings", admin, raised)).StatusCode);
        // The configured minimum is a warning threshold, not a transaction gate.
        var belowWarning = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "threshold-warning");
        Assert.Equal(HttpStatusCode.OK, belowWarning.StatusCode);
        Assert.Equal("awaiting_shopper_confirmation", (await belowWarning.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());
        var lowered = FinancialSettingsRequest(current.GetProperty("merchantCommissionRatePercent").GetDecimal(), current.GetProperty("creatorSharePercent").GetDecimal(), current.GetProperty("shopperSharePercent").GetDecimal(), current.GetProperty("platformSharePercent").GetDecimal(), 1000m); Assert.Equal(HttpStatusCode.OK, (await Put(client, "/api/v1/admin/financial-settings", admin, lowered)).StatusCode);
        var allowed = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "threshold-allowed"); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); Assert.Equal("awaiting_shopper_confirmation", (await allowed.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString()); Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot());
        var ownerAccountId = Guid.Parse("20000000-0000-0000-0000-000000000008"); Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/accounts/{ownerAccountId}/suspend", admin, new { reason = "Security review" })).StatusCode); Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/accounts/{ownerAccountId}/reactivate", admin, new { reason = "Review complete" })).StatusCode);
        await using var db = Db(); Assert.Equal(1000m, await db.BusinessTypeWalletMinimumVersions.Where(x => x.BusinessType == "Other").OrderByDescending(x => x.VersionNumber).Select(x => x.MinimumBusinessWalletBalance).FirstAsync()); Assert.True(await db.CommissionAuditEvents.CountAsync(x => x.EventType == "FinancialSettingsChanged") >= 2); Assert.Equal(AccountStatus.Active, (await db.UserAccounts.SingleAsync(x => x.Id == ownerAccountId)).Status); Assert.True(await db.OperationalAuditEvents.CountAsync(x => x.SubjectId == ownerAccountId) >= 2);
    }

    [DockerFact]
    public async Task Platform_admin_can_correct_business_type_and_business_profile_stays_read_only()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");

        var opsCreate = await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "OperationsAdmin",
            email = "ops-business-type@e2e.invalid",
            phoneNumber = "0911000886",
            password = "E2e-test-password-1!",
            confirmation = "E2e-test-password-1!"
        });
        Assert.Equal(HttpStatusCode.Created, opsCreate.StatusCode);
        var opsLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ops-business-type@e2e.invalid", password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, opsLogin.StatusCode);
        var opsToken = (await opsLogin.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("accessToken").GetString()!;

        await SetBusinessTypeMinimumAsync("Electronics", 2000m);
        await SetBusinessTypeMinimumAsync("Other", 1200m);

        var before = await Get(client, "/api/v1/admin/accounts?page=1&pageSize=100&role=MerchantAdmin", admin);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeBody = await before.Content.ReadFromJsonAsync<JsonElement>();
        var merchantRow = beforeBody!.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("businessName").GetString() == "Active E2E Business");
        Assert.Equal("Other", merchantRow.GetProperty("merchantBusinessType").GetString());
        Assert.Equal(1200m, merchantRow.GetProperty("requiredMinimum").GetDecimal());

        Assert.Equal(HttpStatusCode.Forbidden, (await Put(client, "/api/v1/admin/merchants/20000000-0000-0000-0000-000000000001/business-type", opsToken, new { businessType = "Electronics" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Put(client, "/api/v1/admin/merchants/20000000-0000-0000-0000-000000000001/business-type", admin, new { businessType = "Electronics" })).StatusCode);

        var after = await Get(client, "/api/v1/admin/accounts?page=1&pageSize=100&role=MerchantAdmin", admin);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var afterBody = await after.Content.ReadFromJsonAsync<JsonElement>();
        var updatedRow = afterBody!.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("businessName").GetString() == "Active E2E Business");
        Assert.Equal("Electronics", updatedRow.GetProperty("merchantBusinessType").GetString());
        Assert.Equal(2000m, updatedRow.GetProperty("requiredMinimum").GetDecimal());

        var profile = await Get(client, "/api/v1/merchants/me", owner);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var profileBody = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Electronics", profileBody!.GetProperty("businessType").GetString());

        var updateAttempt = await Put(client, "/api/v1/merchants/me", owner, new
        {
            legalBusinessName = profileBody.GetProperty("legalBusinessName").GetString()!,
            tradingName = profileBody.GetProperty("tradingName").GetString()!,
            businessType = "Furniture",
            taxRegistrationNumber = profileBody.GetProperty("taxRegistrationNumber").ValueKind == JsonValueKind.Null ? null : profileBody.GetProperty("taxRegistrationNumber").GetString(),
            phoneNumber = profileBody.GetProperty("phoneNumber").GetString()!,
            email = profileBody.GetProperty("email").GetString()!,
            businessAddress = profileBody.GetProperty("businessAddress").GetString()!,
            city = profileBody.GetProperty("city").GetString()!,
            region = profileBody.GetProperty("region").GetString()!,
            country = profileBody.GetProperty("country").GetString()!,
            timeZone = profileBody.GetProperty("timeZone").GetString()!,
            logo = (object?)null,
            documents = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, updateAttempt.StatusCode);
        Assert.Contains("Business type cannot be changed after registration.", await updateAttempt.Content.ReadAsStringAsync());

        var refreshedProfile = await Get(client, "/api/v1/merchants/me", owner);
        Assert.Equal(HttpStatusCode.OK, refreshedProfile.StatusCode);
        Assert.Equal("Electronics", (await refreshedProfile.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("businessType").GetString());
    }

    [DockerFact]
    public async Task Platform_admin_can_query_operations_admin_accounts_and_operations_admin_cannot_create_accounts()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");

        var operationsList = await Get(client, "/api/v1/admin/accounts?page=1&pageSize=100&role=OperationsAdmin", admin);
        Assert.Equal(HttpStatusCode.OK, operationsList.StatusCode);
        _ = await operationsList.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        var created = await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "OperationsAdmin",
            displayName = "Pilot Operations Admin",
            email = "ops-admin@e2e.invalid",
            phoneNumber = "0911000999",
            password = "E2e-test-password-1!",
            confirmation = "E2e-test-password-1!"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var page = await (await Get(client, "/api/v1/admin/accounts?page=1&pageSize=100&role=OperationsAdmin", admin)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(page!.GetProperty("items").EnumerateArray(), x => x.GetProperty("email").GetString() == "ops-admin@e2e.invalid" && x.GetProperty("name").GetString() == "Pilot Operations Admin" && x.GetProperty("role").GetString() == "OperationsAdmin");

        var nameSearch = await (await Get(client, "/api/v1/admin/accounts?page=1&pageSize=100&role=OperationsAdmin&q=Pilot%20Operations", admin)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains(nameSearch!.GetProperty("items").EnumerateArray(), x => x.GetProperty("email").GetString() == "ops-admin@e2e.invalid");

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ops-admin@e2e.invalid", password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var opsToken = loginBody!.GetProperty("accessToken").GetString()!;

        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/v1/admin/dashboard/trends", opsToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/v1/admin/accounts?page=1&pageSize=100&role=PlatformAdmin", opsToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/api/v1/admin/accounts/create", opsToken, new
        {
            role = "PlatformAdmin",
            displayName = "Forbidden Escalation",
            email = "should-not-create@e2e.invalid",
            password = "E2e-test-password-1!",
            confirmation = "E2e-test-password-1!"
        })).StatusCode);
    }

    [DockerFact]
    public async Task Operations_admin_can_login_and_bootstrap_its_session()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");

        var created = await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "OperationsAdmin",
            email = "session-ops-admin@e2e.invalid",
            phoneNumber = "0911000888",
            password = "E2e-test-password-1!",
            confirmation = "E2e-test-password-1!"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "session-ops-admin@e2e.invalid", password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("OperationsAdmin", body!.GetProperty("user").GetProperty("role").GetString());

        var opsToken = body.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opsToken);
        var me = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/auth/me");
        Assert.Equal("OperationsAdmin", me!.GetProperty("role").GetString());
        Assert.Equal("Active", me.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/api/v1/admin/accounts/create", opsToken, new
        {
            role = "Customer",
            email = "ops-cannot-create@e2e.invalid",
            phoneNumber = "0911000666",
            password = "E2e-test-password-1!",
            confirmation = "E2e-test-password-1!"
        })).StatusCode);
    }

    [DockerFact]
    public async Task Operations_admin_can_access_operational_admin_endpoints_but_not_platform_only_pages()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var created = await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "OperationsAdmin",
            email = "ops-access@e2e.invalid",
            phoneNumber = "0911000667",
            password = "E2e-test-password-1!",
            confirmation = "E2e-test-password-1!"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "ops-access@e2e.invalid", password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var opsToken = body!.GetProperty("accessToken").GetString()!;

        foreach (var path in new[]
        {
            "/api/v1/creators/pending",
            "/api/v1/admin/merchants/pending",
            "/api/v1/admin/payout-cycles/creators",
            "/api/v1/admin/payout-cycles/shoppers",
            "/api/v1/admin/payout-cycles/platform-revenue",
            "/api/v1/admin/payout-history/creators?page=1&pageSize=25",
            "/api/v1/admin/payout-history/shoppers?page=1&pageSize=25",
            "/api/v1/admin/deposits",
            "/api/v1/admin/wallets?page=1&pageSize=25",
            "/api/v1/admin/fraud-alerts",
            "/api/v1/admin/disputes",
            "/api/v1/admin/reversals"
        })
        {
            var response = await Get(client, path, opsToken);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Path failed: {path} returned {response.StatusCode}. Body: {responseBody}");
        }

        foreach (var path in new[]
        {
            "/api/v1/admin/dashboard/summary",
            "/api/v1/admin/financial-settings",
            "/api/v1/admin/system"
        })
        {
            var response = await Get(client, path, opsToken);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"Path failed: {path} returned {response.StatusCode}. Body: {responseBody}");
        }

        foreach (var path in new[]
        {
            "/api/v1/admin/accounts?page=1&pageSize=25&role=MerchantAdmin",
            "/api/v1/admin/accounts?page=1&pageSize=25&role=Creator",
            "/api/v1/admin/accounts?page=1&pageSize=25&role=Customer",
            "/api/v1/admin/accounts?page=1&pageSize=25&role=Cashier"
        })
        {
            var response = await Get(client, path, opsToken);
            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"Path failed: {path} returned {response.StatusCode}. Body: {responseBody}");
        }

        foreach (var path in new[]
        {
            "/api/v1/admin/dashboard/trends",
            "/api/v1/admin/reports/financial-summary",
            "/api/v1/admin/accounts?page=1&pageSize=25&role=PlatformAdmin"
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, path, opsToken)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/api/v1/admin/accounts/create", opsToken, new
        {
            role = "Customer",
            email = "should-not-create@e2e.invalid",
            phoneNumber = "0911000777",
            password = "E2e-test-password-1!",
            confirmation = "E2e-test-password-1!"
        })).StatusCode);
    }

    [DockerFact]
    public async Task Merchant_admin_checkout_records_owner_actor_and_preserves_cashier_flow()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var otherOwner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000099", merchantId: "20000000-0000-0000-0000-000000000002");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var creatorCode = seed.GetProperty("creatorCode").GetString()!;
        var ownerUserId = Guid.Parse("20000000-0000-0000-0000-000000000008");
        var cashierUserId = Guid.Parse("20000000-0000-0000-0000-000000000006");
        var cashierEntityId = Guid.Parse("20000000-0000-0000-0000-000000000005");

        var cashierValidation = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashier, new { creatorCode });
        Assert.Equal(HttpStatusCode.OK, cashierValidation.StatusCode);
        Assert.True((await cashierValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());

        var cashierCheckout = await Post(client, "/api/v1/cashier/checkouts/by-creator", cashier, new { creatorCode, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "cashier-owner-regression");
        Assert.Equal(HttpStatusCode.OK, cashierCheckout.StatusCode);
        var cashierBody = await cashierCheckout.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("awaiting_shopper_confirmation", cashierBody!.GetProperty("code").GetString());
        var cashierCheckoutId = cashierBody.GetProperty("checkout").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{cashierCheckoutId}/approve", shopper, new { }, "cashier-owner-regression-confirm")).StatusCode);

        var ownerValidation = await Post(client, "/api/v1/merchant/checkouts/validate-creator", owner, new { creatorCode });
        Assert.Equal(HttpStatusCode.OK, ownerValidation.StatusCode);
        Assert.True((await ownerValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());
        var otherOwnerValidation = await Post(client, "/api/v1/merchant/checkouts/validate-creator", otherOwner, new { creatorCode });
        Assert.Equal(HttpStatusCode.OK, otherOwnerValidation.StatusCode);
        Assert.False((await otherOwnerValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());

        var ownerCheckout = await Post(client, "/api/v1/merchant/checkouts/by-creator", owner, new { creatorCode, shopperPhoneNumber = "0911000001", purchaseAmount = 125m }, "owner-checkout");
        Assert.Equal(HttpStatusCode.OK, ownerCheckout.StatusCode);
        var ownerBody = await ownerCheckout.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("awaiting_shopper_confirmation", ownerBody!.GetProperty("code").GetString());
        var ownerCheckoutId = ownerBody.GetProperty("checkout").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{ownerCheckoutId}/approve", shopper, new { }, "owner-checkout-confirm")).StatusCode);

        await using (var db = Db())
        {
            var cashierSession = await db.CheckoutSessions.SingleAsync(x => x.Id == cashierCheckoutId);
            var ownerSession = await db.CheckoutSessions.SingleAsync(x => x.Id == ownerCheckoutId);
            Assert.NotNull(cashierSession.PurchaseTransactionId);
            Assert.NotNull(ownerSession.PurchaseTransactionId);
            var cashierPurchase = await db.PurchaseTransactions.SingleAsync(x => x.Id == cashierSession.PurchaseTransactionId!.Value);
            var ownerPurchase = await db.PurchaseTransactions.SingleAsync(x => x.Id == ownerSession.PurchaseTransactionId!.Value);
            Assert.Equal(cashierEntityId, cashierPurchase.CashierId);
            Assert.Equal(cashierUserId, cashierPurchase.CreatedByUserId);
            Assert.Null(ownerPurchase.CashierId);
            Assert.Equal(ownerUserId, ownerPurchase.CreatedByUserId);
            Assert.Equal(2, await db.PurchaseTransactions.CountAsync());
            Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "FourPartyPurchaseConfirmed" && x.SubjectId == cashierPurchase.Id && x.ActorUserId == cashierUserId));
            Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "FourPartyPurchaseConfirmed" && x.SubjectId == ownerPurchase.Id && x.ActorUserId == ownerUserId));
        }

        var confirmedSales = await Get(client, "/api/v1/merchant/confirmed-sales?page=1&pageSize=10", owner);
        Assert.Equal(HttpStatusCode.OK, confirmedSales.StatusCode);
        var confirmedSalesBody = await confirmedSales.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var sales = confirmedSalesBody!.GetProperty("sales").EnumerateArray().ToList();
        Assert.Equal(2, sales.Count);
        Assert.Contains(sales, x => x.GetProperty("cashierName").GetString()!.Contains("Business Owner", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/api/v1/merchant/checkouts/by-creator", Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001"), new { creatorCode, shopperPhoneNumber = "0911000001", purchaseAmount = 75m }, "creator-cannot-checkout")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/api/v1/merchant/checkouts/by-creator", shopper, new { creatorCode, shopperPhoneNumber = "0911000001", purchaseAmount = 75m }, "customer-cannot-checkout")).StatusCode);
    }

    [DockerFact]
    public async Task Platform_admin_sees_cashier_business_and_location_and_disabling_preserves_history()
    {
        using var client = factory!.CreateClient();
        var seededResponse = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seededResponse.StatusCode);
        var seed = await seededResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var cashierToken = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");

        var accounts = await Get(client, "/api/v1/admin/accounts?page=1&pageSize=100", admin);
        Assert.Equal(HttpStatusCode.OK, accounts.StatusCode);
        var page = await accounts.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("role").GetString() == "Cashier");
        Assert.Equal("E2E Cashier", cashier.GetProperty("cashierName").GetString());
        Assert.Equal("cashier@e2e.invalid", cashier.GetProperty("email").GetString());
        Assert.Equal("+251911000005", cashier.GetProperty("phone").GetString());
        Assert.Equal("Active E2E Business", cashier.GetProperty("businessName").GetString());
        Assert.Equal("E2E Checkout", cashier.GetProperty("assignedLocation").GetString());

        var businessCashiers = await Get(client, "/api/v1/admin/merchants/20000000-0000-0000-0000-000000000001/cashiers?page=1&pageSize=100", admin);
        Assert.Equal(HttpStatusCode.OK, businessCashiers.StatusCode);
        var businessPage = await businessCashiers.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Single(businessPage.GetProperty("items").EnumerateArray());
        Assert.Equal("Active E2E Business", businessPage.GetProperty("items")[0].GetProperty("businessName").GetString());
        var otherBusiness = await Get(client, "/api/v1/admin/merchants/20000000-0000-0000-0000-000000000002/cashiers?page=1&pageSize=100", admin);
        Assert.Empty((await otherBusiness.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/v1/admin/cashiers", owner)).StatusCode);

        var submitted = await Post(client, "/api/v1/cashier/checkouts/offer", cashierToken, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "cashier-history");
        var checkout = await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var checkoutId = checkout.GetProperty("checkout").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{checkoutId}/approve", shopper, new { }, "cashier-history-confirm")).StatusCode);
        Guid transactionId;
        await using (var before = Db()) transactionId = await before.PurchaseTransactions.Select(x => x.Id).SingleAsync();

        var cashierAccountId = cashier.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/accounts/{cashierAccountId}/deactivate", admin, new { reason = "Cashier disabled for test" })).StatusCode);
        await using (var disabled = Db())
        {
            Assert.False((await disabled.Cashiers.SingleAsync(x => x.Id == Guid.Parse("20000000-0000-0000-0000-000000000005"))).IsActive);
            Assert.Equal(AccountStatus.Closed, (await disabled.UserAccounts.SingleAsync(x => x.Id == cashierAccountId)).Status);
            Assert.Equal(MerchantStatus.Active, (await disabled.Merchants.SingleAsync(x => x.Id == Guid.Parse("20000000-0000-0000-0000-000000000001"))).Status);
            Assert.True(await disabled.PurchaseTransactions.AnyAsync(x => x.Id == transactionId));
        }
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/accounts/{cashierAccountId}/reactivate", admin, new { reason = "Cashier restored" })).StatusCode);
        await using var reactivated = Db();
        Assert.True((await reactivated.Cashiers.SingleAsync(x => x.Id == Guid.Parse("20000000-0000-0000-0000-000000000005"))).IsActive);
        Assert.True(await reactivated.PurchaseTransactions.AnyAsync(x => x.Id == transactionId));
    }

    [DockerFact]
    public async Task Platform_admin_can_create_supported_accounts_without_dob_or_location_and_creation_is_audited()
    {
        using var client = factory!.CreateClient();
        var seededResponse = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seededResponse.StatusCode);
        var admin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");

        var creatorResponse = await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "Creator",
            email = "created-creator@e2e.invalid",
            phoneNumber = "0911000991",
            password = "Created-account-1!",
            confirmation = "Created-account-1!",
            firstName = "Created",
            lastName = "Creator",
            displayName = "Created Creator"
        }, "create-creator");
        Assert.Equal(HttpStatusCode.Created, creatorResponse.StatusCode);
        var creatorBody = await creatorResponse.Content.ReadFromJsonAsync<JsonElement>();
        var creatorAccountId = creatorBody.GetProperty("accountId").GetGuid();

        var customerResponse = await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "Customer",
            email = "created-shopper@e2e.invalid",
            phoneNumber = "0911000992",
            password = "Created-account-2!",
            confirmation = "Created-account-2!",
            displayName = "Created Shopper"
        }, "create-customer");
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        var customerAccountId = (await customerResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accountId").GetGuid();

        var merchantResponse = await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "MerchantAdmin",
            email = "created-business@e2e.invalid",
            phoneNumber = "0911000993",
            password = "Created-account-3!",
            confirmation = "Created-account-3!",
            legalBusinessName = "Created Business LLC",
            tradingName = "Created Business",
            businessType = "Other",
            primaryContactName = "Created Owner",
            businessAddress = "123 Admin Street",
            city = "Addis Ababa",
            region = "Addis Ababa",
            country = "Ethiopia",
            timeZone = "Africa/Addis_Ababa"
        }, "create-merchant");
        Assert.Equal(HttpStatusCode.Created, merchantResponse.StatusCode);
        var merchantBody = await merchantResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdMerchantId = merchantBody.GetProperty("merchantId").GetGuid();
        var merchantAccountId = merchantBody.GetProperty("accountId").GetGuid();

        var cashierResponse = await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "Cashier",
            email = "created-cashier@e2e.invalid",
            phoneNumber = "0911000994",
            password = "Created-account-4!",
            confirmation = "Created-account-4!",
            firstName = "Created",
            lastName = "Cashier",
            merchantId = createdMerchantId
        }, "create-cashier");
        Assert.Equal(HttpStatusCode.Created, cashierResponse.StatusCode);
        var cashierBody = await cashierResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cashierAccountId = cashierBody.GetProperty("accountId").GetGuid();

        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "Customer",
            email = "created-shopper@e2e.invalid",
            phoneNumber = "0911000995",
            password = "Created-account-5!",
            confirmation = "Created-account-5!",
            displayName = "Duplicate Email"
        }, "create-duplicate-email")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, "/api/v1/admin/accounts/create", admin, new
        {
            role = "Customer",
            email = "created-shopper-2@e2e.invalid",
            phoneNumber = "0911000992",
            password = "Created-account-6!",
            confirmation = "Created-account-6!",
            displayName = "Duplicate Phone"
        }, "create-duplicate-phone")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/api/v1/admin/accounts/create", owner, new
        {
            role = "Creator",
            email = "blocked@e2e.invalid",
            phoneNumber = "0911000996",
            password = "Created-account-7!",
            confirmation = "Created-account-7!",
            firstName = "Blocked",
            lastName = "User",
            displayName = "Blocked User"
        }, "blocked-create")).StatusCode);

        await using var db = Db();
        var creator = await db.UserAccounts.SingleAsync(x => x.Id == creatorAccountId);
        var customer = await db.UserAccounts.SingleAsync(x => x.Id == customerAccountId);
        var merchantAccount = await db.UserAccounts.SingleAsync(x => x.Id == merchantAccountId);
        var cashier = await db.UserAccounts.SingleAsync(x => x.Id == cashierAccountId);
        Assert.Equal(AccountStatus.Active, creator.Status);
        Assert.Equal(AccountStatus.Active, customer.Status);
        Assert.Equal(AccountStatus.Active, merchantAccount.Status);
        Assert.Equal(AccountStatus.Active, cashier.Status);
        Assert.Null(creator.BirthDate);
        Assert.Null(customer.BirthDate);
        Assert.Null(merchantAccount.BirthDate);
        Assert.Null(cashier.BirthDate);
        Assert.True(cashier.IsEmailVerified);
        Assert.True(cashier.IsPhoneVerified);
        Assert.Equal(createdMerchantId, cashier.MerchantId);
        Assert.Empty(await db.CashierLocationAssignments.Where(x => x.CashierId == cashier.CashierId).ToListAsync());
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "AdminAccountCreated" && x.SubjectId == cashierAccountId));
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "AdminAccountCreated" && x.SubjectId == creatorAccountId));
    }

    [DockerFact]
    public async Task Admin_delete_anonymizes_creator_and_customer_accounts_without_breaking_financial_history()
    {
        using var client = factory!.CreateClient();
        var seededResponse = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seededResponse.StatusCode);

        var platformAdmin = Token(UserRole.PlatformAdmin, "90000000-0000-0000-0000-000000000001");
        var operationsAdmin = Token(UserRole.OperationsAdmin, "90000000-0000-0000-0000-000000000002");
        var creatorEmail = "delete-creator@e2e.invalid";
        var customerEmail = "delete-customer@e2e.invalid";
        var merchantEmail = "delete-business@e2e.invalid";
        var cashierEmail = "delete-cashier@e2e.invalid";

        var creatorCreate = await Post(client, "/api/v1/admin/accounts/create", platformAdmin, new
        {
            role = "Creator",
            email = creatorEmail,
            phoneNumber = "0911000333",
            password = "Created-account-10!",
            confirmation = "Created-account-10!",
            firstName = "Delete",
            lastName = "Creator",
            displayName = "Delete Creator"
        }, "delete-creator-create");
        Assert.Equal(HttpStatusCode.Created, creatorCreate.StatusCode);
        var creatorBody = await creatorCreate.Content.ReadFromJsonAsync<JsonElement>();
        var creatorAccountId = creatorBody!.GetProperty("accountId").GetGuid();

        var customerCreate = await Post(client, "/api/v1/admin/accounts/create", platformAdmin, new
        {
            role = "Customer",
            email = customerEmail,
            phoneNumber = "0911000334",
            password = "Created-account-11!",
            confirmation = "Created-account-11!",
            displayName = "Delete Customer"
        }, "delete-customer-create");
        Assert.Equal(HttpStatusCode.Created, customerCreate.StatusCode);
        var customerAccountId = (await customerCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accountId").GetGuid();

        var merchantCreate = await Post(client, "/api/v1/admin/accounts/create", platformAdmin, new
        {
            role = "MerchantAdmin",
            email = merchantEmail,
            phoneNumber = "0911000335",
            password = "Created-account-12!",
            confirmation = "Created-account-12!",
            legalBusinessName = "Delete Business LLC",
            tradingName = "Delete Business",
            businessType = "Other",
            primaryContactName = "Delete Business Owner",
            businessAddress = "123 Delete Street",
            city = "Addis Ababa",
            region = "Addis Ababa",
            country = "Ethiopia",
            timeZone = "Africa/Addis_Ababa"
        }, "delete-business-create");
        Assert.Equal(HttpStatusCode.Created, merchantCreate.StatusCode);
        var merchantBody = await merchantCreate.Content.ReadFromJsonAsync<JsonElement>();
        var merchantAccountId = merchantBody!.GetProperty("accountId").GetGuid();
        var createdMerchantId = merchantBody.GetProperty("merchantId").GetGuid();

        var cashierCreate = await Post(client, "/api/v1/admin/accounts/create", platformAdmin, new
        {
            role = "Cashier",
            email = cashierEmail,
            phoneNumber = "0911000336",
            password = "Created-account-13!",
            confirmation = "Created-account-13!",
            firstName = "Delete",
            lastName = "Cashier",
            merchantId = createdMerchantId
        }, "delete-cashier-create");
        Assert.Equal(HttpStatusCode.Created, cashierCreate.StatusCode);
        var cashierAccountId = (await cashierCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accountId").GetGuid();

        await using (var created = Db())
        {
            Assert.Equal(AccountStatus.Active, await created.UserAccounts.Where(x => x.Id == creatorAccountId).Select(x => x.Status).SingleAsync());
            Assert.Equal(AccountStatus.Active, await created.UserAccounts.Where(x => x.Id == customerAccountId).Select(x => x.Status).SingleAsync());
            Assert.Equal(AccountStatus.Active, await created.UserAccounts.Where(x => x.Id == merchantAccountId).Select(x => x.Status).SingleAsync());
            Assert.Equal(AccountStatus.Active, await created.UserAccounts.Where(x => x.Id == cashierAccountId).Select(x => x.Status).SingleAsync());
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, $"/api/v1/admin/accounts/{creatorAccountId}/delete", operationsAdmin, new { reason = "Operations admin cannot delete creator accounts" }, "delete-creator-forbidden")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, $"/api/v1/admin/accounts/{customerAccountId}/delete", operationsAdmin, new { reason = "Operations admin cannot delete customer accounts" }, "delete-customer-forbidden")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, $"/api/v1/admin/accounts/{merchantAccountId}/delete", operationsAdmin, new { reason = "Operations admin cannot delete business accounts" }, "delete-business-forbidden")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, $"/api/v1/admin/accounts/{cashierAccountId}/delete", operationsAdmin, new { reason = "Operations admin cannot delete cashier accounts" }, "delete-cashier-forbidden")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/accounts/{cashierAccountId}/delete", platformAdmin, new { reason = "Cashier account deleted for test" }, "delete-cashier")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/accounts/{merchantAccountId}/delete", platformAdmin, new { reason = "Business account deleted for test" }, "delete-business")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/accounts/{creatorAccountId}/delete", platformAdmin, new { reason = "Creator account deleted for test" }, "delete-creator")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, $"/api/v1/admin/accounts/{customerAccountId}/delete", platformAdmin, new { reason = "Customer account deleted for test" }, "delete-customer")).StatusCode);

        var creatorAttempt = await Post(client, $"/api/v1/admin/accounts/{customerAccountId}/delete", Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001"), new { reason = "Creator cannot delete accounts" }, "delete-customer-forbidden");
        Assert.Equal(HttpStatusCode.Forbidden, creatorAttempt.StatusCode);

        await using var db = Db();
        var creator = await db.UserAccounts.SingleAsync(x => x.Id == creatorAccountId);
        var customer = await db.UserAccounts.SingleAsync(x => x.Id == customerAccountId);
        var merchantAccount = await db.UserAccounts.SingleAsync(x => x.Id == merchantAccountId);
        var cashierAccount = await db.UserAccounts.SingleAsync(x => x.Id == cashierAccountId);
        Assert.Equal(AccountStatus.Closed, creator.Status);
        Assert.Equal(AccountStatus.Closed, customer.Status);
        Assert.Equal(AccountStatus.Closed, merchantAccount.Status);
        Assert.Equal(AccountStatus.Closed, cashierAccount.Status);
        Assert.StartsWith($"deleted+{creatorAccountId:N}@", creator.Email);
        Assert.StartsWith($"deleted+{customerAccountId:N}@", customer.Email);
        Assert.StartsWith($"deleted+{merchantAccountId:N}@", merchantAccount.Email);
        Assert.StartsWith($"deleted+{cashierAccountId:N}@", cashierAccount.Email);
        Assert.Null(creator.PhoneNumber);
        Assert.Null(customer.PhoneNumber);
        Assert.Null(merchantAccount.PhoneNumber);
        Assert.Null(cashierAccount.PhoneNumber);
        Assert.Equal(string.Empty, creator.PasswordHash);
        Assert.Equal(string.Empty, customer.PasswordHash);
        Assert.Equal(string.Empty, merchantAccount.PasswordHash);
        Assert.Equal(string.Empty, cashierAccount.PasswordHash);
        Assert.False(creator.IsEmailVerified);
        Assert.False(customer.IsEmailVerified);
        Assert.False(merchantAccount.IsEmailVerified);
        Assert.False(cashierAccount.IsEmailVerified);
        Assert.False(creator.IsPhoneVerified);
        Assert.False(customer.IsPhoneVerified);
        Assert.False(merchantAccount.IsPhoneVerified);
        Assert.False(cashierAccount.IsPhoneVerified);
        Assert.Equal("admin-delete", creator.UpdatedBy);
        Assert.Equal("admin-delete", customer.UpdatedBy);
        Assert.Equal("admin-delete", merchantAccount.UpdatedBy);
        Assert.Equal("admin-delete", cashierAccount.UpdatedBy);
        Assert.Equal(0, await db.RefreshTokens.CountAsync(x => x.UserAccountId == creatorAccountId && x.RevokedAtUtc == null));
        Assert.Equal(0, await db.RefreshTokens.CountAsync(x => x.UserAccountId == customerAccountId && x.RevokedAtUtc == null));
        Assert.Equal(0, await db.RefreshTokens.CountAsync(x => x.UserAccountId == merchantAccountId && x.RevokedAtUtc == null));
        Assert.Equal(0, await db.RefreshTokens.CountAsync(x => x.UserAccountId == cashierAccountId && x.RevokedAtUtc == null));
        Assert.Equal(CreatorStatus.Closed, (await db.Creators.SingleAsync(x => x.Id == creator.CreatorId!.Value)).Status);
        Assert.Equal(CustomerStatus.Closed, (await db.Customers.SingleAsync(x => x.Id == customer.CustomerId!.Value)).Status);
        Assert.Equal(MerchantStatus.Closed, (await db.Merchants.SingleAsync(x => x.Id == merchantAccount.MerchantId!.Value)).Status);
        Assert.False((await db.Cashiers.SingleAsync(x => x.Id == cashierAccount.CashierId!.Value)).IsActive);
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "AdminAccountDelete" && x.SubjectId == creatorAccountId));
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "AdminAccountDelete" && x.SubjectId == customerAccountId));
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "AdminAccountDelete" && x.SubjectId == merchantAccountId));
        Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "AdminAccountDelete" && x.SubjectId == cashierAccountId));

        var creatorLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = creatorEmail, password = "Created-account-10!" });
        var customerLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = customerEmail, password = "Created-account-11!" });
        var merchantLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = merchantEmail, password = "Created-account-12!" });
        var cashierLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = cashierEmail, password = "Created-account-13!" });
        Assert.NotEqual(HttpStatusCode.OK, creatorLogin.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, customerLogin.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, merchantLogin.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, cashierLogin.StatusCode);

        var forbiddenDelete = await Post(client, $"/api/v1/admin/accounts/{creatorAccountId}/delete", Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001"), new { reason = "Creator cannot delete admin accounts" });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, $"/api/v1/admin/accounts/90000000-0000-0000-0000-000000000001/delete", platformAdmin, new { reason = "Platform admin self-delete blocked" })).StatusCode);
    }

    [DockerFact]
    public async Task Shopper_rejection_is_idempotent_and_posts_no_money()
    {
        using var client = factory!.CreateClient(); var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" }); Assert.Equal(HttpStatusCode.OK, seeded.StatusCode); var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005"); var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        var submitted = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), shopperPhoneNumber = "0911000001", purchaseAmount = 500m }, "shopper-rejects"); var body = await submitted.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal("awaiting_shopper_confirmation", body.GetProperty("code").GetString()); var id = body.GetProperty("checkout").GetProperty("id").GetGuid(); Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot());
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{id}/reject", shopper, new { })).StatusCode); Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{id}/reject", shopper, new { })).StatusCode); Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot()); await using var db = Db(); Assert.Equal(CheckoutSessionStatus.Rejected, (await db.CheckoutSessions.SingleAsync(x => x.Id == id)).Status); Assert.True(await db.OperationalAuditEvents.AnyAsync(x => x.EventType == "ShopperRejectedPurchase" && x.SubjectId == id));
    }

    [DockerFact]
    public async Task Expired_relationship_blocks_permanent_qr_and_new_checkout_without_changing_financial_history()
    {
        using var client = factory!.CreateClient();
        var seeded = await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" });
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
        var seed = await seeded.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var creator = Token(UserRole.Creator, "30000000-0000-0000-0000-000000000004", creatorId: "30000000-0000-0000-0000-000000000001");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: "20000000-0000-0000-0000-000000000001");
        var cashier = Token(UserRole.Cashier, "20000000-0000-0000-0000-000000000006", merchantId: "20000000-0000-0000-0000-000000000001", cashierId: "20000000-0000-0000-0000-000000000005");
        var location = seed.GetProperty("locationId").GetGuid();

        DateTime activated;
        await using (var db = Db())
        {
            var relationship = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == Guid.Parse("40000000-0000-0000-0000-000000000001"));
            activated = relationship.StartDateUtc!.Value;
            Assert.NotEqual(relationship.ApprovedAtUtc, relationship.StartDateUtc);
            Assert.Equal(activated.AddDays(MerchantCreatorPartnership.ActivePeriodDays), relationship.EndDateUtc);
            Assert.True(relationship.IsTransactionEligibleAt(activated.AddDays(29)));
            var expires = relationship.EndDateUtc!.Value;
            Assert.False(relationship.IsTransactionEligibleAt(expires));
            Assert.False(relationship.IsTransactionEligibleAt(expires.AddTicks(1)));
        }

        var qrResponse = await Get(client, "/api/v1/creator/qr/", creator);
        Assert.Equal(HttpStatusCode.OK, qrResponse.StatusCode);
        var permanentQr = await qrResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var qrPayload = permanentQr.GetProperty("payload").GetString()!;
        var activeValidation = await Post(client, "/api/v1/merchant/qr/validate", owner, new { payload = qrPayload, locationId = location });
        Assert.True((await activeValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("isValid").GetBoolean());

        var direct = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "expiration-history-sale");
        var directBody = await direct.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("awaiting_shopper_confirmation", directBody.GetProperty("code").GetString());
        Assert.Equal((0, 0, 0, 0, 0, 0, 0), await Snapshot());
        var shopper = Token(UserRole.Customer, "10000000-0000-0000-0000-000000000002", customerId: "10000000-0000-0000-0000-000000000001");
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/customer/checkouts/{directBody.GetProperty("checkout").GetProperty("id").GetGuid()}/approve", shopper, new { }, "confirm-expiration-history-sale")).StatusCode);
        var financialHistory = await Snapshot();
        await using (var db = Db())
        {
            Assert.Single(await db.PurchaseTransactions.AsNoTracking().ToListAsync());
            Assert.Single(await db.CreatorEarnings.AsNoTracking().ToListAsync());
            Assert.Single(await db.CustomerCashbackEntries.AsNoTracking().Where(x => x.EntryType == CustomerCashbackEntryType.Earned).ToListAsync());
            Assert.Single(await db.PlatformRevenueEntries.AsNoTracking().ToListAsync());
            Assert.Single(await db.FinancialJournals.AsNoTracking().Where(x => x.IsPosted).ToListAsync());
            Assert.Equal(4, await db.FinancialJournalLines.AsNoTracking().CountAsync());
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE merchant_creator_partnerships SET \"EndDateUtc\" = {DateTime.UtcNow.AddSeconds(-1)} WHERE \"Id\" = {Guid.Parse("40000000-0000-0000-0000-000000000001")}");
        }

        var expiredValidation = await Post(client, "/api/v1/merchant/qr/validate", owner, new { payload = qrPayload, locationId = location });
        var validationBody = await expiredValidation.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.False(validationBody.GetProperty("isValid").GetBoolean());
        Assert.Equal("PartnershipExpired", validationBody.GetProperty("code").GetString());
        await using (var db = Db()) Assert.True(await db.CreatorQrCodes.AnyAsync(x => x.Id == permanentQr.GetProperty("id").GetGuid() && x.IsActive));

        var rejected = await Post(client, "/api/v1/cashier/checkouts/offer", cashier, new { qrPayload = seed.GetProperty("offerQrPayload").GetString(), merchantLocationId = location, shopperPhoneNumber = "0911000001", purchaseAmount = 100m }, "expired-relationship-sale");
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(financialHistory, await Snapshot());
        var expiredPerformance = await (await Get(client, "/api/v1/creator/ads/performance", creator)).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var expiredBusiness = expiredPerformance.GetProperty("businesses").EnumerateArray().Single(x => x.GetProperty("partnershipId").GetGuid() == Guid.Parse("40000000-0000-0000-0000-000000000001"));
        Assert.Equal("Expired", expiredBusiness.GetProperty("status").GetString()); Assert.Equal(1, expiredBusiness.GetProperty("confirmedSales").GetInt32()); Assert.Equal(4m, expiredBusiness.GetProperty("creatorEarned").GetDecimal()); Assert.Single(expiredPerformance.GetProperty("transactions").EnumerateArray());
        await using (var db = Db())
        {
            Assert.Equal(100m, await db.PurchaseTransactions.Select(x => x.PurchaseAmount).SingleAsync());
            Assert.Equal(4m, await db.CreatorEarnings.Select(x => x.Amount).SingleAsync());
            Assert.Equal(3m, await db.CustomerCashbackEntries.Where(x => x.EntryType == CustomerCashbackEntryType.Earned).Select(x => x.Amount).SingleAsync());
            Assert.Equal(3m, await db.PlatformRevenueEntries.Select(x => x.Amount).SingleAsync());
            Assert.True(await db.MerchantAuditEvents.AnyAsync(x => x.EventType == "CreatorQrValidationFailed"));
        }
    }

    private async Task<Guid> CreateAndPresent(HttpClient client, string shopper, string cashier, string key, decimal amount = 100m)
    {
        var create = await Post(client, "/api/v1/customer/checkouts/by-offer", shopper, new { offerCode = "E2EACTIVE" }, $"create-{key}");
        Assert.True(create.StatusCode == HttpStatusCode.Created, $"{create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        var checkout = await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = checkout.GetProperty("id").GetGuid();
        await using var db = Db();
        var row = await db.CheckoutSessions.SingleAsync(x => x.Id == id);
        row.PurchaseAmount = amount;
        row.ExpectedCreatorAmount = amount * .04m;
        row.ExpectedCashbackAmount = amount * .03m;
        row.CashierId = Guid.Parse("20000000-0000-0000-0000-000000000005");
        row.MerchantLocationId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        row.PresentedAtUtc = DateTime.UtcNow;
        row.PresentIdempotencyKey = $"present-{key}";
        row.Status = CheckoutSessionStatus.AwaitingCustomerApproval;
        await db.SaveChangesAsync();
        return id;
    }

    [DockerFact]
    public async Task Cashier_creation_and_invitations_allow_no_location_and_preserve_optional_assignment()
    {
        using var client = factory!.CreateClient(); var merchantId = Guid.NewGuid(); var ownerId = Guid.NewGuid();
        await using (var db = Db())
        {
            db.Add(new Merchant { Id = merchantId, PublicMerchantId = $"MER-{Guid.NewGuid():N}"[..20], LegalBusinessName = "No Location Trading PLC", TradingName = "No Location Trading", BusinessType = "Other", PrimaryContactName = "Owner", PhoneNumber = "0911555010", NormalizedPhoneNumber = "251911555010", Email = "no-location-owner@e2e.invalid", PreferredLanguage = "en", TermsAcceptedAtUtc = DateTime.UtcNow, BusinessAddress = "Bole Road", City = "Addis Ababa", Region = "Addis Ababa", Country = "Ethiopia", TimeZone = "Africa/Addis_Ababa", Status = MerchantStatus.Active, CreatedAtUtc = DateTime.UtcNow });
            db.Add(new UserAccount { Id = ownerId, Email = "no-location-owner@e2e.invalid", NormalizedEmail = "NO-LOCATION-OWNER@E2E.INVALID", Role = UserRole.MerchantAdmin, Status = AccountStatus.Active, MerchantId = merchantId, IsEmailVerified = true, CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync();
            Assert.False(await db.MerchantLocations.AnyAsync(x => x.MerchantId == merchantId));
        }
        var owner = Token(UserRole.MerchantAdmin, ownerId.ToString(), merchantId: merchantId.ToString());
        foreach (var input in new[] { (Email: "default-cashier-one@e2e.invalid", Phone: "0911222333"), (Email: "default-cashier-two@e2e.invalid", Phone: "0911222444") })
        {
            var response = await Post(client, "/api/v1/merchant/cashiers/invitations", owner, new { firstName = "Default", lastName = "Cashier", email = input.Email, phoneNumber = input.Phone, locationName = (string?)null });
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        }
        const string temporaryPassword = "Temporary1!Secure"; var created = await Post(client, "/api/v1/merchant/cashiers", owner, new { firstName = "Direct", lastName = "Cashier", phoneNumber = "0911222555", temporaryPassword, confirmation = temporaryPassword }); Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var duplicate = await Post(client, "/api/v1/merchant/cashiers", owner, new { firstName = "Other", lastName = "Cashier", phoneNumber = "+251911222555", temporaryPassword, confirmation = temporaryPassword }); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "+251911222555", password = temporaryPassword }); Assert.Equal(HttpStatusCode.OK, login.StatusCode); var loginBody = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); Assert.Equal("Cashier", loginBody.GetProperty("user").GetProperty("role").GetString());
        await using (var verify = Db()) { Assert.False(await verify.MerchantLocations.AnyAsync(x => x.MerchantId == merchantId)); Assert.Equal(0, await verify.CashierLocationAssignments.CountAsync()); var account = await verify.UserAccounts.SingleAsync(x => x.NormalizedPhoneNumber == "+251911222555"); Assert.False(account.IsPhoneVerified); Assert.Null(account.BirthDate); Assert.Equal(AccountStatus.Active, account.Status); Assert.NotEqual(temporaryPassword, account.PasswordHash); Assert.DoesNotContain(temporaryPassword, account.PasswordHash); }
        var locationResponse = await Post(client, "/api/v1/merchant/locations", owner, new { name = "Bole Branch", addressLine1 = "Bole Road", city = "Addis Ababa", countryCode = "ET", timeZoneId = "Africa/Addis_Ababa" }); Assert.Equal(HttpStatusCode.Created, locationResponse.StatusCode); var locationId = (await locationResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var assigned = await Post(client, "/api/v1/merchant/cashiers", owner, new { firstName = "Assigned", lastName = "Cashier", phoneNumber = "0911222666", temporaryPassword, confirmation = temporaryPassword, locationId }); Assert.Equal(HttpStatusCode.OK, assigned.StatusCode); var assignedId = (await assigned.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        await using (var verify = Db()) { var direct = await verify.Cashiers.SingleAsync(x => x.NormalizedPhoneNumber == "+251911222555"); Assert.Equal(HttpStatusCode.OK, (await Put(client, $"/api/v1/merchant/cashiers/{direct.Id}/locations", owner, new { locationIds = new[] { locationId }, primaryLocationId = locationId })).StatusCode); Assert.Equal(locationId, (await verify.CashierLocationAssignments.SingleAsync(x => x.CashierId == direct.Id && x.IsActive)).MerchantLocationId); Assert.Equal(locationId, (await verify.CashierLocationAssignments.SingleAsync(x => x.CashierId == assignedId && x.IsActive)).MerchantLocationId); }
    }

    [DockerFact]
    public async Task Cashier_invitation_is_scoped_single_use_expiring_and_assigns_the_selected_location()
    {
        using var client = factory!.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/e2e/seed", new { password = "E2e-test-password-1!" })).StatusCode);
        var merchantId = "20000000-0000-0000-0000-000000000001";
        var locationId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        var owner = Token(UserRole.MerchantAdmin, "20000000-0000-0000-0000-000000000008", merchantId: merchantId);
        var invite = await Post(client, "/api/v1/merchant/cashiers/invitations", owner, new { firstName = "Pilot", lastName = "Cashier", email = "pilot-cashier@e2e.invalid", phoneNumber = "0911222333", locationIds = new[] { locationId } });
        Assert.True(invite.StatusCode == HttpStatusCode.Created, await invite.Content.ReadAsStringAsync());
        var invitation = await invite.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var invitationId = invitation.GetProperty("invitationId").GetGuid();
        var url = new Uri(invitation.GetProperty("invitationUrl").GetString()!);
        var raw = Uri.UnescapeDataString(url.Query.Split("token=", 2)[1]);

        var preview = await client.GetAsync($"/api/v1/staff-invitations/preview?token={Uri.EscapeDataString(raw)}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var details = await preview.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Cashier", details.GetProperty("role").GetString());
        await using (var db = Db())
        {
            var pending = await db.StaffInvitations.SingleAsync(x => x.Id == invitationId);
            Assert.NotEqual(raw, pending.TokenHash);
            Assert.Null(pending.AcceptedAtUtc);
            var cashier = await db.Cashiers.SingleAsync(x => x.Id == pending.CashierId);
            Assert.False(cashier.IsActive);
            Assert.False(await db.UserAccounts.AnyAsync(x => x.CashierId == cashier.Id));
            var assignment = await db.CashierLocationAssignments.SingleAsync(x => x.CashierId == cashier.Id);
            Assert.Equal(locationId, assignment.MerchantLocationId);
            Assert.True(assignment.IsPrimary);
        }

        var other = Token(UserRole.MerchantAdmin, Guid.NewGuid().ToString(), merchantId: Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await Post(client, $"/api/v1/merchant/staff-invitations/{invitationId}/revoke", other, new { })).StatusCode);
        var accepted = await client.PostAsJsonAsync("/api/v1/staff-invitations/accept", new { token = raw, password = "Cashier-test-1!", confirmation = "Cashier-test-1!" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/staff-invitations/accept", new { token = raw, password = "Cashier-test-1!", confirmation = "Cashier-test-1!" })).StatusCode);
        await using (var db = Db())
        {
            var pending = await db.StaffInvitations.SingleAsync(x => x.Id == invitationId);
            var user = await db.UserAccounts.SingleAsync(x => x.CashierId == pending.CashierId);
            Assert.Equal(UserRole.Cashier, user.Role); Assert.Equal(AccountStatus.Active, user.Status); Assert.Equal(Guid.Parse(merchantId), user.MerchantId);
            Assert.True(await db.MerchantAuditEvents.AnyAsync(x => x.EventType == "CashierInvitationAccepted"));
        }

        var expiredInvite = await Post(client, "/api/v1/merchant/cashiers/invitations", owner, new { firstName = "Expired", lastName = "Cashier", email = "expired-cashier@e2e.invalid", phoneNumber = "0911222444", locationIds = new[] { locationId } });
        var expiredJson = await expiredInvite.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var expiredId = expiredJson.GetProperty("invitationId").GetGuid();
        var expiredRaw = Uri.UnescapeDataString(new Uri(expiredJson.GetProperty("invitationUrl").GetString()!).Query.Split("token=", 2)[1]);
        await using (var db = Db()) { (await db.StaffInvitations.SingleAsync(x => x.Id == expiredId)).ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); }
        var expiredAccept = await client.PostAsJsonAsync("/api/v1/staff-invitations/accept", new { token = expiredRaw, password = "Cashier-test-1!", confirmation = "Cashier-test-1!" });
        Assert.Equal(HttpStatusCode.BadRequest, expiredAccept.StatusCode); Assert.Contains("expired", await expiredAccept.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var revokedInvite = await Post(client, "/api/v1/merchant/cashiers/invitations", owner, new { firstName = "Revoked", lastName = "Cashier", email = "revoked-cashier@e2e.invalid", phoneNumber = "0911222555", locationIds = new[] { locationId } });
        var revokedJson = await revokedInvite.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var revokedId = revokedJson.GetProperty("invitationId").GetGuid();
        var revokedRaw = Uri.UnescapeDataString(new Uri(revokedJson.GetProperty("invitationUrl").GetString()!).Query.Split("token=", 2)[1]);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, $"/api/v1/merchant/staff-invitations/{revokedId}/revoke", owner, new { })).StatusCode);
        var revokedAccept = await client.PostAsJsonAsync("/api/v1/staff-invitations/accept", new { token = revokedRaw, password = "Cashier-test-1!", confirmation = "Cashier-test-1!" });
        Assert.Equal(HttpStatusCode.BadRequest, revokedAccept.StatusCode); Assert.Contains("revoked", await revokedAccept.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        await using var finalDb = Db();
        var cashierRow = await finalDb.Cashiers.SingleAsync(x => x.NormalizedEmail == "PILOT-CASHIER@E2E.INVALID");
        var cashierUser = await finalDb.UserAccounts.SingleAsync(x => x.CashierId == cashierRow.Id);
        var cashierToken = Token(UserRole.Cashier, cashierUser.Id.ToString(), merchantId: merchantId, cashierId: cashierRow.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/v1/merchant/wallet", cashierToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get(client, "/api/v1/admin/deposits/pending", cashierToken)).StatusCode);
    }

    private async Task<(int Purchases, int WalletEntries, int Earnings, int Cashback, int Snapshots, int Revenue, int Journals)> Snapshot()
    {
        await using var db = Db();
        return (await db.PurchaseTransactions.CountAsync(), await db.MerchantWalletEntries.CountAsync(), await db.CreatorEarnings.CountAsync(), await db.CustomerCashbackEntries.CountAsync(), await db.CommissionCalculationSnapshots.CountAsync(), await db.PlatformRevenueEntries.CountAsync(), await db.FinancialJournals.CountAsync());
    }

    private async Task<decimal> WalletBalance() { await using var db = Db(); return await db.MerchantWallets.Where(x => x.MerchantId == Guid.Parse("20000000-0000-0000-0000-000000000001")).Select(x => x.AvailableBalance).SingleAsync(); }
    private static async Task<System.Text.Json.JsonElement> ValidateCreator(HttpClient client, string cashierToken, string creatorCode)
    {
        var response = await Post(client, "/api/v1/cashier/checkouts/validate-creator", cashierToken, new { creatorCode });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
    }
    private async Task AssertDeactivated(bool expectFinancialHistory = false)
    {
        await using var db = Db(); var relationship = await db.MerchantCreatorPartnerships.SingleAsync(x => x.Id == Guid.Parse("40000000-0000-0000-0000-000000000001"));
        Assert.Equal(PartnershipStatus.Revoked, relationship.Status); Assert.False(relationship.IsTransactionEligibleAt(DateTime.UtcNow)); Assert.NotNull(relationship.UpdatedAtUtc); Assert.NotNull(relationship.UpdatedBy);
        Assert.True(await db.CreatorQrCodes.AnyAsync(x => x.CreatorId == relationship.CreatorId && x.IsActive));
        if (expectFinancialHistory) { Assert.Single(await db.PurchaseTransactions.ToListAsync()); Assert.Single(await db.CreatorEarnings.ToListAsync()); Assert.Single(await db.CustomerCashbackEntries.Where(x => x.EntryType == CustomerCashbackEntryType.Earned).ToListAsync()); }
        else { Assert.Empty(await db.PurchaseTransactions.ToListAsync()); Assert.Empty(await db.CreatorEarnings.ToListAsync()); Assert.Empty(await db.CustomerCashbackEntries.ToListAsync()); }
    }

    private static async Task<HttpResponseMessage> Deposit(HttpClient client, string token, string key, decimal amount, string fileName, string contentType, byte[] bytes)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(amount.ToString(CultureInfo.InvariantCulture)), "amount"); var file = new ByteArrayContent(bytes); file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType); form.Add(file, "proof", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/merchant/deposits") { Content = form }; request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request);
    }
    private static byte[] Png() => [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];
    private static byte[] Jpeg() => [255, 216, 255, 224, 0, 0, 0, 0];

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
    private static async Task<HttpResponseMessage> Put(HttpClient client, string path, string token, object body) { using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) }; request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return await client.SendAsync(request); }

    private static object FinancialSettingsRequest(decimal merchantCommissionRatePercent, decimal creatorSharePercent, decimal shopperSharePercent, decimal platformSharePercent, decimal minimumBusinessWalletBalance, bool applyNow = true, DateTime? effectiveFromUtc = null)
        => new
        {
            merchantCommissionRatePercent,
            creatorSharePercent,
            shopperSharePercent,
            platformSharePercent,
            businessTypeMinimumWalletBalances = BusinessTypes.Values.Select(type => new { businessType = type, minimumBusinessWalletBalance }).ToArray(),
            minimumTikTokFollowers = 0,
            applyNow,
            effectiveFromUtc,
            creatorCutoffDay = "Friday",
            creatorCutoffTime = "00:00:00",
            creatorPayoutDay = "Saturday",
            shopperCutoffDay = 0,
            shopperCutoffTime = "00:00:00",
            shopperPayoutDay = 1
        };

    private async Task SetBusinessTypeMinimumAsync(string businessType, decimal minimum)
    {
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();
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

    private async Task SetAllBusinessTypeMinimumsAsync(decimal minimum)
    {
        foreach (var businessType in BusinessTypes.Values) await SetBusinessTypeMinimumAsync(businessType, minimum);
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
