using CreatorPay.Application.Authentication;
using CreatorPay.Application.Merchants;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Application.Qr;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Infrastructure.Qr;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Api.Testing;

public sealed record E2eSeedRequest(string Password);
public sealed record E2eLockedPinUserRequest(string PhoneNumber, string Password, string Pin);

public static class E2eSeedEndpoints
{
    public static IEndpointRouteBuilder MapE2eSeedEndpoints(this IEndpointRouteBuilder endpoints, IWebHostEnvironment environment)
    {
        if (!environment.IsEnvironment("E2E") && !environment.IsEnvironment("Test")) return endpoints;
        endpoints.MapPost("/api/v1/e2e/seed", SeedAsync);
        endpoints.MapPost("/api/v1/e2e/locked-pin-user", SeedLockedPinUserAsync);
        return endpoints;
    }

    private static async Task<IResult> SeedLockedPinUserAsync(E2eLockedPinUserRequest request, ApplicationDbContext db, IPasswordHasher passwords, CancellationToken ct)
    {
        if (request.Password.Length < 12 || request.Pin.Length != 5 || request.Pin.Any(x => !char.IsAsciiDigit(x))) return Results.BadRequest();
        var phone = EthiopianMobileNumber.Normalize(request.PhoneNumber);
        if (await db.UserAccounts.AnyAsync(x => x.NormalizedPhoneNumber == phone, ct)) return Results.Conflict();
        var now = DateTime.UtcNow;
        var customer = new Customer { Id = Guid.NewGuid(), PublicCustomerId = $"CUS-PIN-{Guid.NewGuid():N}"[..20], DisplayName = "Locked PIN E2E", PhoneNumber = phone, NormalizedPhoneNumber = phone, CreatedAtUtc = now };
        var user = new UserAccount { Id = Guid.NewGuid(), PhoneNumber = phone, NormalizedPhoneNumber = phone, Role = UserRole.Customer, Status = AccountStatus.Active, CustomerId = customer.Id, PinVersion = 1, PinEnrolledAtUtc = now, PinChangedAtUtc = now, PinFailedAttemptCount = 10, PinLockedAtUtc = now, CreatedAtUtc = now };
        user.PasswordHash = passwords.Hash(user, request.Password);
        user.PinHash = passwords.Hash(user, $"weymela-pin-v1:{request.Pin}");
        db.AddRange(customer, user, new CustomerWallet { Id = Guid.NewGuid(), CustomerId = customer.Id, CurrencyCode = "ETB", CreatedAtUtc = now });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { phoneNumber = phone });
    }

    private static async Task<IResult> SeedAsync(E2eSeedRequest request, ApplicationDbContext db, IPasswordHasher passwords, IQrTokenService qrTokens, CreatorQrUrlBuilder qrUrls, IWebHostEnvironment environment, CancellationToken ct)
    {
        if (request.Password.Length < 12) return Results.BadRequest();
        if (await db.UserAccounts.AnyAsync(x => x.Email == "shopper@e2e.invalid", ct)) return Results.Conflict();
        var now = DateTime.UtcNow; Guid Id(string value) => Guid.Parse(value);
        foreach (var businessType in BusinessTypes.Values)
        {
            db.BusinessTypeWalletMinimumVersions.Add(new BusinessTypeWalletMinimumVersion
            {
                Id = Guid.NewGuid(),
                CurrencyCode = "ETB",
                BusinessType = businessType,
                VersionNumber = 1,
                MinimumBusinessWalletBalance = 0m,
                EffectiveFromUtc = now,
                ChangedByUserId = Id("90000000-0000-0000-0000-000000000001"),
                CreatedAtUtc = now,
                CreatedBy = "90000000-0000-0000-0000-000000000001"
            });
        }
        var shopper = new Customer { Id = Id("10000000-0000-0000-0000-000000000001"), PublicCustomerId = "CUS-E2E", DisplayName = "E2E Shopper", PhoneNumber = "+251911000001", NormalizedPhoneNumber = "+251911000001", CreatedAtUtc = now };
        var user = new UserAccount { Id = Id("10000000-0000-0000-0000-000000000002"), Email = "shopper@e2e.invalid", NormalizedEmail = "SHOPPER@E2E.INVALID", PhoneNumber = shopper.PhoneNumber, NormalizedPhoneNumber = shopper.NormalizedPhoneNumber, Role = UserRole.Customer, Status = AccountStatus.Active, CustomerId = shopper.Id, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; user.PasswordHash = passwords.Hash(user, request.Password);
        var merchant = Merchant(Id("20000000-0000-0000-0000-000000000001"), "Active E2E Business", MerchantStatus.Active, now); merchant.PhoneNumber = merchant.NormalizedPhoneNumber = "+251933000001";
        var ownerUser = new UserAccount { Id = Id("20000000-0000-0000-0000-000000000008"), Email = "owner@e2e.invalid", NormalizedEmail = "OWNER@E2E.INVALID", PhoneNumber = merchant.PhoneNumber, NormalizedPhoneNumber = merchant.NormalizedPhoneNumber, Role = UserRole.MerchantAdmin, Status = AccountStatus.Active, MerchantId = merchant.Id, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; ownerUser.PasswordHash = passwords.Hash(ownerUser, request.Password);
        var inactiveMerchant = Merchant(Id("20000000-0000-0000-0000-000000000002"), "Inactive E2E Business", MerchantStatus.Suspended, now);
        var location = new MerchantLocation { Id = Id("20000000-0000-0000-0000-000000000003"), MerchantId = merchant.Id, Name = "E2E Checkout", AddressLine1 = "Synthetic", City = "Addis Ababa", CountryCode = "ET", TimeZoneId = "Africa/Addis_Ababa", IsActive = true, CreatedAtUtc = now };
        var cashier = new Cashier { Id = Id("20000000-0000-0000-0000-000000000005"), MerchantId = merchant.Id, FirstName = "E2E", LastName = "Cashier", Email = "cashier@e2e.invalid", NormalizedEmail = "CASHIER@E2E.INVALID", PhoneNumber = "+251911000005", NormalizedPhoneNumber = "+251911000005", IsActive = true, CreatedAtUtc = now };
        var cashierUser = new UserAccount { Id = Id("20000000-0000-0000-0000-000000000006"), Email = cashier.Email, NormalizedEmail = cashier.NormalizedEmail, PhoneNumber = cashier.PhoneNumber, NormalizedPhoneNumber = cashier.NormalizedPhoneNumber, Role = UserRole.Cashier, Status = AccountStatus.Active, MerchantId = merchant.Id, CashierId = cashier.Id, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; cashierUser.PasswordHash = passwords.Hash(cashierUser, request.Password);
        var cashierAssignment = new CashierLocationAssignment { Id = Id("20000000-0000-0000-0000-000000000007"), CashierId = cashier.Id, MerchantLocationId = location.Id, IsActive = true, IsPrimary = true, CreatedAtUtc = now };
        var active = Creator(Id("30000000-0000-0000-0000-000000000001"), "Selam Active", CreatorStatus.Active, now); active.CreatorCode = "4827"; active.PhoneNumber = active.NormalizedPhoneNumber = "+251944999999";
        var creatorQrPublicId = "creator-e2e-permanent"; var creatorQrToken = qrTokens.CreateToken(creatorQrPublicId, 1); var creatorQr = new CreatorQrCode { Id = Id("30000000-0000-0000-0000-000000000015"), CreatorId = active.Id, PublicQrId = creatorQrPublicId, TokenHash = qrTokens.Hash(creatorQrToken), Version = 1, IssuedAtUtc = now, CreatedAtUtc = now };
        var activeCreatorUser = new UserAccount { Id = Id("30000000-0000-0000-0000-000000000004"), Email = "creator@e2e.invalid", NormalizedEmail = "CREATOR@E2E.INVALID", PhoneNumber = active.PhoneNumber, NormalizedPhoneNumber = active.NormalizedPhoneNumber, Role = UserRole.Creator, Status = AccountStatus.Active, CreatorId = active.Id, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; activeCreatorUser.PasswordHash = passwords.Hash(activeCreatorUser, request.Password);
        var adminUser = new UserAccount { Id = Id("90000000-0000-0000-0000-000000000001"), Email = "admin@e2e.invalid", NormalizedEmail = "ADMIN@E2E.INVALID", Role = UserRole.PlatformAdmin, Status = AccountStatus.Active, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; adminUser.PasswordHash = passwords.Hash(adminUser, request.Password);
        var workflowEntities = new List<object>();
        var confirmationShoppers = new List<object>();
        var confirmationAccounts = new List<object>();
        var confirmationShopperEmails = new Dictionary<string, string>();
        var confirmationShopperPhones = new Dictionary<string, string>();
        foreach (var (key, index) in new[] { ("yes-desktop", 1), ("no-desktop", 2), ("yes-mobile", 3), ("no-mobile", 4) })
        {
            var customerId = Id($"11000000-0000-0000-0000-00000000000{index}");
            var accountId = Id($"11000000-0000-0000-0000-00000000001{index}");
            var phone = $"+25192200000{index}";
            var confirmationShopper = new Customer { Id = customerId, PublicCustomerId = $"CUS-CONFIRM-{index}", DisplayName = $"E2E {key} Shopper", PhoneNumber = phone, NormalizedPhoneNumber = phone, CreatedAtUtc = now };
            var confirmationAccount = new UserAccount { Id = accountId, Email = $"shopper-{key}@e2e.invalid", NormalizedEmail = $"SHOPPER-{key.ToUpperInvariant()}@E2E.INVALID", PhoneNumber = phone, NormalizedPhoneNumber = phone, Role = UserRole.Customer, Status = AccountStatus.Active, CustomerId = customerId, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; confirmationAccount.PasswordHash = passwords.Hash(confirmationAccount, request.Password);
            confirmationShoppers.AddRange([confirmationShopper, new CustomerWallet { Id = Guid.NewGuid(), CustomerId = customerId, CurrencyCode = "ETB", CreatedAtUtc = now }]);
            confirmationAccounts.Add(confirmationAccount);
            confirmationShopperEmails[key] = confirmationAccount.Email;
            confirmationShopperPhones[key] = phone;
        }
        foreach (var (suffix, label) in new[] { ("1", "Desktop"), ("2", "Mobile") })
        {
            var businessId = Id($"21000000-0000-0000-0000-00000000000{suffix}"); var businessPhone = $"+25193400000{suffix}"; var business = Merchant(businessId, $"{label} Workflow Business", MerchantStatus.Active, now); business.PublicMerchantId = $"MER-WORKFLOW-{suffix}"; business.PhoneNumber = business.NormalizedPhoneNumber = businessPhone; var businessUser = new UserAccount { Id = Id($"21000000-0000-0000-0000-00000000001{suffix}"), Email = $"business-{suffix}@e2e.invalid", NormalizedEmail = $"BUSINESS-{suffix}@E2E.INVALID", PhoneNumber = businessPhone, NormalizedPhoneNumber = businessPhone, Role = UserRole.MerchantAdmin, Status = AccountStatus.Active, MerchantId = businessId, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; businessUser.PasswordHash = passwords.Hash(businessUser, request.Password);
            var requestCreatorId = Id($"31000000-0000-0000-0000-00000000000{suffix}"); var requestCreatorPhone = $"+25194400000{suffix}"; var requestCreator = Creator(requestCreatorId, $"{label} Request Creator", CreatorStatus.Active, now); requestCreator.CreatorCode = $"510{suffix}"; requestCreator.PublicCreatorId = $"CRE-REQUEST-{suffix}"; requestCreator.PhoneNumber = requestCreator.NormalizedPhoneNumber = requestCreatorPhone; var requestCreatorUser = new UserAccount { Id = Id($"31000000-0000-0000-0000-00000000001{suffix}"), Email = $"creator-request-{suffix}@e2e.invalid", NormalizedEmail = $"CREATOR-REQUEST-{suffix}@E2E.INVALID", PhoneNumber = requestCreatorPhone, NormalizedPhoneNumber = requestCreatorPhone, Role = UserRole.Creator, Status = AccountStatus.Active, CreatorId = requestCreatorId, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; requestCreatorUser.PasswordHash = passwords.Hash(requestCreatorUser, request.Password);
            var inviteCreatorId = Id($"32000000-0000-0000-0000-00000000000{suffix}"); var inviteCreatorPhone = $"+25195500000{suffix}"; var inviteCreator = Creator(inviteCreatorId, $"{label} Invite Creator", CreatorStatus.Active, now); inviteCreator.CreatorCode = $"520{suffix}"; inviteCreator.PublicCreatorId = $"CRE-INVITE-{suffix}"; inviteCreator.PhoneNumber = inviteCreator.NormalizedPhoneNumber = inviteCreatorPhone; var inviteCreatorUser = new UserAccount { Id = Id($"32000000-0000-0000-0000-00000000001{suffix}"), Email = $"creator-invite-{suffix}@e2e.invalid", NormalizedEmail = $"CREATOR-INVITE-{suffix}@E2E.INVALID", PhoneNumber = inviteCreatorPhone, NormalizedPhoneNumber = inviteCreatorPhone, Role = UserRole.Creator, Status = AccountStatus.Active, CreatorId = inviteCreatorId, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; inviteCreatorUser.PasswordHash = passwords.Hash(inviteCreatorUser, request.Password);
            var pendingCreatorId = Id($"33000000-0000-0000-0000-00000000000{suffix}"); var pendingCreatorPhone = $"+25196600000{suffix}"; var pendingCreator = Creator(pendingCreatorId, $"{label} Pending Creator", CreatorStatus.PendingReview, now); pendingCreator.CreatorCode = $"530{suffix}"; pendingCreator.PublicCreatorId = $"CRE-PENDING-{suffix}"; pendingCreator.PhoneNumber = pendingCreator.NormalizedPhoneNumber = pendingCreatorPhone; var pendingCreatorUser = new UserAccount { Id = Id($"33000000-0000-0000-0000-00000000001{suffix}"), Email = $"creator-pending-{suffix}@e2e.invalid", NormalizedEmail = $"CREATOR-PENDING-{suffix}@E2E.INVALID", PhoneNumber = pendingCreatorPhone, NormalizedPhoneNumber = pendingCreatorPhone, Role = UserRole.Creator, Status = AccountStatus.PendingApproval, CreatorId = pendingCreatorId, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; pendingCreatorUser.PasswordHash = passwords.Hash(pendingCreatorUser, request.Password);
            workflowEntities.AddRange([business, businessUser, requestCreator, requestCreatorUser, inviteCreator, inviteCreatorUser, pendingCreator, pendingCreatorUser]);
        }
        var noOffer = Creator(Id("30000000-0000-0000-0000-000000000002"), "No Offer Creator", CreatorStatus.Active, now); noOffer.CreatorCode = "4828";
        var noOfferQrPublicId = "creator-e2e-no-campaign"; var noOfferQrToken = qrTokens.CreateToken(noOfferQrPublicId, 1); var noOfferQr = new CreatorQrCode { Id = Id("30000000-0000-0000-0000-000000000025"), CreatorId = noOffer.Id, PublicQrId = noOfferQrPublicId, TokenHash = qrTokens.Hash(noOfferQrToken), Version = 1, IssuedAtUtc = now, CreatedAtUtc = now };
        var suspended = Creator(Id("30000000-0000-0000-0000-000000000003"), "Suspended Creator", CreatorStatus.Suspended, now); suspended.CreatorCode = "4829";
        active.SocialProfiles.Add(new() { Id = Id("30000000-0000-0000-0000-000000000011"), CreatorId = active.Id, Platform = SocialPlatform.TikTok, ProfileUrl = "https://www.tiktok.com/@weymela-e2e", Handle = "@weymela-e2e", CreatedAtUtc = now });
        active.SocialProfiles.Add(new() { Id = Id("30000000-0000-0000-0000-000000000012"), CreatorId = active.Id, Platform = SocialPlatform.YouTube, ProfileUrl = "https://www.youtube.com/@weymela-e2e", Handle = "@weymela-e2e", CreatedAtUtc = now });
        active.SocialProfiles.Add(new() { Id = Id("30000000-0000-0000-0000-000000000013"), CreatorId = active.Id, Platform = SocialPlatform.Other, ProfileUrl = "javascript:alert(1)", Handle = "unsafe", CreatedAtUtc = now });
        var partnership = new MerchantCreatorPartnership { Id = Id("40000000-0000-0000-0000-000000000001"), MerchantId = merchant.Id, CreatorId = active.Id, RequestedAtUtc = now, CreatedAtUtc = now }; partnership.Approve(now, user.Id); partnership.ActivatePromotion(now.AddMinutes(-1), user.Id);
        var promotion = new PromotionVideo { Id = Id("40000000-0000-0000-0000-000000000011"), MerchantCreatorPartnershipId = partnership.Id, MerchantId = merchant.Id, CreatorId = active.Id, VideoUrl = "https://www.tiktok.com/@weymela-e2e/video/1234567890123456789", SubmittedAtUtc = now.AddMinutes(-2), CreatedAtUtc = now.AddMinutes(-2) }; promotion.Approve(now.AddMinutes(-1), user.Id);
        var secondCreator = Creator(Id("30000000-0000-0000-0000-000000000021"), "Mimi Active", CreatorStatus.Active, now); secondCreator.CreatorCode = "4830"; secondCreator.PublicCreatorId = "CRE-E2E-SECOND";
        var secondCreatorUser = new UserAccount { Id = Id("30000000-0000-0000-0000-000000000022"), Email = "creator-second@e2e.invalid", NormalizedEmail = "CREATOR-SECOND@E2E.INVALID", Role = UserRole.Creator, Status = AccountStatus.Active, CreatorId = secondCreator.Id, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now }; secondCreatorUser.PasswordHash = passwords.Hash(secondCreatorUser, request.Password);
        var secondQrPublicId = "creator-e2e-second"; var secondQr = new CreatorQrCode { Id = Id("30000000-0000-0000-0000-000000000023"), CreatorId = secondCreator.Id, PublicQrId = secondQrPublicId, TokenHash = qrTokens.Hash(qrTokens.CreateToken(secondQrPublicId, 1)), Version = 1, IssuedAtUtc = now, CreatedAtUtc = now };
        secondCreator.SocialProfiles.Add(new() { Id = Id("30000000-0000-0000-0000-000000000024"), CreatorId = secondCreator.Id, Platform = SocialPlatform.Instagram, Handle = "@mimi-e2e", FollowerCount = 35000, IsPrimary = true, CreatedAtUtc = now });
        var secondPartnership = new MerchantCreatorPartnership { Id = Id("40000000-0000-0000-0000-000000000002"), MerchantId = merchant.Id, CreatorId = secondCreator.Id, RequestedAtUtc = now, CreatedAtUtc = now }; secondPartnership.Approve(now, user.Id); secondPartnership.ActivatePromotion(now.AddMinutes(-1), user.Id);
        var secondPromotion = new PromotionVideo { Id = Id("40000000-0000-0000-0000-000000000012"), MerchantCreatorPartnershipId = secondPartnership.Id, MerchantId = merchant.Id, CreatorId = secondCreator.Id, VideoUrl = "https://www.tiktok.com/@mimi-e2e/video/1234567890123456790", SubmittedAtUtc = now.AddMinutes(-2), CreatedAtUtc = now.AddMinutes(-2) }; secondPromotion.Approve(now.AddMinutes(-1), user.Id);
        var plan = new CommissionPlan { Id = Id("50000000-0000-0000-0000-000000000001"), Name = "E2E", CreatedAtUtc = now };
        var rule = new CommissionRule { Id = Id("50000000-0000-0000-0000-000000000002"), CommissionPlanId = plan.Id, Name = "E2E 10%", ScopeType = CommissionScopeType.CampaignOverride, CurrencyCode = "ETB", IsActive = true, CreatedAtUtc = now };
        var version = new CommissionRuleVersion { Id = Id("50000000-0000-0000-0000-000000000003"), CommissionRuleId = rule.Id, VersionNumber = 1, MerchantCommissionRatePercent = 10, CreatorSharePercent = 40, CustomerCashbackSharePercent = 30, PlatformSharePercent = 30, EffectiveFromUtc = now.AddDays(-10), RoundingMode = CommissionRoundingMode.AwayFromZero, IsActive = true, CreatedByUserId = user.Id, CreatedAtUtc = now };
        var offer = Campaign(Id("60000000-0000-0000-0000-000000000001"), "CMP-E2E-ACTIVE", active.Id, merchant.Id, partnership.Id, version.Id, "E2EACTIVE", now.AddMinutes(-1), 30, user.Id, qrTokens);
        var secondOffer = Campaign(Id("60000000-0000-0000-0000-000000000003"), "CMP-E2E-SECOND", secondCreator.Id, merchant.Id, secondPartnership.Id, version.Id, "E2ESECOND", now.AddMinutes(-1), 30, user.Id, qrTokens);
        var expired = Campaign(Id("60000000-0000-0000-0000-000000000002"), "CMP-E2E-EXPIRED", active.Id, merchant.Id, partnership.Id, version.Id, "E2EEXPIRED", now.AddDays(-2), 1, user.Id, qrTokens); expired.ExpireIfDue(now);
        var offerQrId = offer.QrCode!.PublicQrId;
        const string expiredRaw = "E2E-EXPIRED-CHECKOUT"; var expiredCheckout = new CheckoutSession { Id = Id("70000000-0000-0000-0000-000000000001"), PublicCheckoutId = "CHK-E2E-EXPIRED", CustomerId = shopper.Id, CampaignId = offer.Id, MerchantId = merchant.Id, CreatorId = active.Id, TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expiredRaw))), ExpiresAtUtc = now.AddMinutes(-1), CreateIdempotencyKey = "e2e-expired", CreatedAtUtc = now.AddMinutes(-5) };
        var wallet = new MerchantWallet { Id = Id("20000000-0000-0000-0000-000000000009"), MerchantId = merchant.Id, CurrencyCode = "ETB", CreatedAtUtc = now }; wallet.Credit(1000m, 100m, now);
        var commissionAssignment = new CampaignCommissionAssignment { Id = Id("50000000-0000-0000-0000-000000000004"), CampaignId = offer.Id, MerchantCreatorPartnershipId = partnership.Id, CommissionRuleId = rule.Id, EffectiveFromUtc = now.AddDays(-10), IsActive = true, CreatedAtUtc = now };
        var platformAssignment = new PlatformCommissionAssignment { Id = Id("50000000-0000-0000-0000-000000000005"), CurrencyCode = "ETB", CommissionRuleId = rule.Id, EffectiveFromUtc = now.AddDays(-10), IsActive = true, CreatedAtUtc = now };
        var creatorBalance = new CreatorBalanceAccount { Id = Id("30000000-0000-0000-0000-000000000014"), CreatorId = active.Id, CurrencyCode = "ETB", CreatedAtUtc = now };
        var e2eFinancialEntities = new List<object>();
        if (environment.IsEnvironment("E2E"))
        {
            var paidAt = now.AddDays(-30);
            var paidSnapshot = FinancialSnapshot(Id("50000000-0000-0000-0000-000000000011"), version, 1500m, 60m, paidAt);
            var upcomingSnapshot = FinancialSnapshot(Id("50000000-0000-0000-0000-000000000012"), version, 1000m, 40m, now.AddDays(-1));
            var paidPurchase = ConfirmedPurchase(Id("70000000-0000-0000-0000-000000000011"), "CP-E2E-PAID", paidSnapshot, 1500m, paidAt);
            var upcomingPurchase = ConfirmedPurchase(Id("70000000-0000-0000-0000-000000000012"), "CP-E2E-UPCOMING", upcomingSnapshot, 1000m, now.AddDays(-1));
            var paidEarning = new CreatorEarning { Id = Id("80000000-0000-0000-0000-000000000011"), CreatorId = active.Id, PurchaseTransactionId = paidPurchase.Id, CommissionCalculationSnapshotId = paidSnapshot.Id, Amount = 60m, CurrencyCode = "ETB", EarnedAtUtc = paidAt, AvailableAtUtc = paidAt, CorrelationId = "e2e-paid-earning", CreatedAtUtc = paidAt };
            var upcomingEarning = new CreatorEarning { Id = Id("80000000-0000-0000-0000-000000000012"), CreatorId = active.Id, PurchaseTransactionId = upcomingPurchase.Id, CommissionCalculationSnapshotId = upcomingSnapshot.Id, Amount = 40m, CurrencyCode = "ETB", EarnedAtUtc = now.AddDays(-1), AvailableAtUtc = now.AddDays(6), CorrelationId = "e2e-upcoming-earning", CreatedAtUtc = now.AddDays(-1) };
            var payoutBatch = new PayoutBatch { Id = Id("80000000-0000-0000-0000-000000000021"), PublicBatchId = "PB-E2E-PAID", CurrencyCode = "ETB", Status = PayoutBatchStatus.Completed, CutoffAtUtc = paidAt.AddDays(-1), ScheduledForUtc = paidAt, CreatedByUserId = adminUser.Id, CompletedAtUtc = paidAt, TotalCreatorCount = 1, TotalAmount = 60m, CorrelationId = "e2e-paid-batch", CreatedAtUtc = paidAt };
            var paidPayout = new CreatorPayout { Id = Id("80000000-0000-0000-0000-000000000022"), PublicPayoutId = "PO-E2E-PAID", PayoutBatchId = payoutBatch.Id, CreatorId = active.Id, CurrencyCode = "ETB", Amount = 60m, Status = CreatorPayoutStatus.Paid, PayoutMethodType = PayoutMethodType.Manual, ScheduledAtUtc = paidAt, SubmittedAtUtc = paidAt, PaidAtUtc = paidAt, IdempotencyKey = "e2e-paid-payout", CorrelationId = "e2e-paid-payout", CreatedAtUtc = paidAt };
            paidEarning.MakeAvailable(paidAt);
            paidEarning.Schedule(paidPayout.Id, paidAt);
            paidEarning.MarkPaid(paidAt);
            paidPayout.Items.Add(new PayoutItem { Id = Id("80000000-0000-0000-0000-000000000023"), CreatorPayoutId = paidPayout.Id, CreatorEarningId = paidEarning.Id, Amount = 60m, CurrencyCode = "ETB", CreatedAtUtc = paidAt });
            payoutBatch.Payouts.Add(paidPayout);
            creatorBalance.CreditPending(60m, paidAt);
            creatorBalance.Transfer(CreatorBalanceCategory.Pending, CreatorBalanceCategory.Available, 60m, paidAt);
            creatorBalance.Transfer(CreatorBalanceCategory.Available, CreatorBalanceCategory.Scheduled, 60m, paidAt);
            creatorBalance.Transfer(CreatorBalanceCategory.Scheduled, CreatorBalanceCategory.Paid, 60m, paidAt);
            creatorBalance.CreditPending(40m, now.AddDays(-1));
            e2eFinancialEntities.AddRange([paidSnapshot, upcomingSnapshot, paidPurchase, upcomingPurchase, paidEarning, upcomingEarning, payoutBatch]);
        }
        db.AddRange(shopper, user, new CustomerWallet { Id = Id("10000000-0000-0000-0000-000000000003"), CustomerId = shopper.Id, CurrencyCode = "ETB", CreatedAtUtc = now }, merchant, ownerUser, inactiveMerchant, location, cashier, cashierUser, cashierAssignment, wallet, active, creatorQr, activeCreatorUser, secondCreator, secondCreatorUser, secondQr, secondPartnership, secondPromotion, secondOffer, adminUser, creatorBalance, new CreatorBalanceAccount { Id = Guid.NewGuid(), CreatorId = secondCreator.Id, CurrencyCode = "ETB", CreatedAtUtc = now }, noOffer, noOfferQr, suspended, partnership, promotion, plan, rule, version, offer, commissionAssignment, platformAssignment, expired, expiredCheckout, new MerchantTrialCredit { Id = Id("20000000-0000-0000-0000-000000000004"), MerchantId = merchant.Id, CreatedAtUtc = now }); db.AddRange(workflowEntities); db.AddRange(confirmationShoppers); db.AddRange(confirmationAccounts); db.AddRange(e2eFinancialEntities);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { shopperEmail = user.Email, shopperPhone = user.PhoneNumber, cashierEmail = cashier.Email, cashierPhone = cashier.PhoneNumber, ownerEmail = ownerUser.Email, ownerPhone = ownerUser.PhoneNumber, creatorEmail = activeCreatorUser.Email, creatorPhone = activeCreatorUser.PhoneNumber, creatorCode = active.CreatorCode, noCampaignCreatorCode = noOffer.CreatorCode, confirmationShopperEmails, confirmationShopperPhones, adminEmail = adminUser.Email, creatorQrPayload = qrUrls.Create(creatorQrPublicId, 1, creatorQrToken), noCampaignQrPayload = qrUrls.Create(noOfferQrPublicId, 1, noOfferQrToken), locationId = location.Id, expiredCheckoutQr = $"creatorpay:checkout:{expiredCheckout.PublicCheckoutId}:{expiredRaw}", creatorName = active.DisplayName, activeOfferCode = offer.CampaignCode, expiredOfferCode = expired.CampaignCode, offerQrId, offerQrPayload = $"creatorpay:offer:{offer.QrCode.PublicQrId}:{qrTokens.CreateToken(offer.QrCode.PublicQrId, 1)}", expiredOfferQrPayload = $"creatorpay:offer:{expired.QrCode!.PublicQrId}:{qrTokens.CreateToken(expired.QrCode.PublicQrId, 1)}" });
    }

    private static Merchant Merchant(Guid id, string name, MerchantStatus status, DateTime now) => new() { Id = id, PublicMerchantId = $"MER-{id.ToString("N")[^16..]}", LegalBusinessName = name, TradingName = name, BusinessType = "Other", PrimaryContactName = "E2E", PhoneNumber = $"+2519{id.ToString("N")[^8..]}", NormalizedPhoneNumber = $"+2519{id.ToString("N")[^8..]}", Email = $"{id:N}@e2e.invalid", TermsAcceptedAtUtc = now, PublicDescription = "Synthetic browser-test Offer.", BusinessAddress = "Synthetic", City = "Addis Ababa", Region = "Addis Ababa", Country = "ET", TimeZone = "Africa/Addis_Ababa", Status = status, CreatedAtUtc = now };
    private static Creator Creator(Guid id, string name, CreatorStatus status, DateTime now) => new() { Id = id, PublicCreatorId = $"CRE-{id.ToString("N")[^16..]}", FirstName = "E2E", LastName = "Creator", DisplayName = name, PhoneNumber = $"+2519{id.ToString("N")[^8..]}", NormalizedPhoneNumber = $"+2519{id.ToString("N")[^8..]}", Email = $"{id:N}@e2e.invalid", City = "Addis Ababa", Biography = "Synthetic", ContentCategories = "Testing", TermsAcceptedAtUtc = now, Status = status, CreatedAtUtc = now };
    private static CommissionCalculationSnapshot FinancialSnapshot(Guid id, CommissionRuleVersion version, decimal purchase, decimal creatorAmount, DateTime now) => new() { Id = id, CommissionRuleId = version.CommissionRuleId, CommissionRuleVersionId = version.Id, CurrencyCode = "ETB", PurchaseAmount = purchase, MerchantCommissionRatePercent = 10m, TotalCommissionAmount = purchase * 0.10m, CreatorSharePercent = 40m, CreatorCommissionAmount = creatorAmount, CustomerCashbackSharePercent = 30m, CustomerCashbackAmount = purchase * 0.03m, PlatformSharePercent = 30m, PlatformCommissionAmount = purchase * 0.03m, RoundingMode = CommissionRoundingMode.AwayFromZero, CalculatedAtUtc = now, RuleSourceType = CommissionRuleSourceType.Campaign, RuleSourceId = version.Id, CreatedAtUtc = now };
    private static PurchaseTransaction ConfirmedPurchase(Guid id, string publicId, CommissionCalculationSnapshot snapshot, decimal amount, DateTime now)
    {
        var purchase = new PurchaseTransaction { Id = id, PublicTransactionId = publicId, CreatorId = Guid.Parse("30000000-0000-0000-0000-000000000001"), CustomerId = Guid.Parse("10000000-0000-0000-0000-000000000001"), MerchantId = Guid.Parse("20000000-0000-0000-0000-000000000001"), MerchantLocationId = Guid.Parse("20000000-0000-0000-0000-000000000003"), CashierId = Guid.Parse("20000000-0000-0000-0000-000000000005"), CreatorQrCodeId = Guid.Parse("30000000-0000-0000-0000-000000000015"), MerchantCreatorPartnershipId = Guid.Parse("40000000-0000-0000-0000-000000000001"), CampaignId = Guid.Parse("60000000-0000-0000-0000-000000000001"), PurchaseAmount = amount, CustomerCashbackAmount = amount * 0.03m, CurrencyCode = "ETB", CommissionCalculationSnapshotId = snapshot.Id, TransactionDateUtc = now, IdempotencyKey = publicId.ToLowerInvariant(), CreatedByUserId = Guid.Parse("20000000-0000-0000-0000-000000000006"), CorrelationId = publicId.ToLowerInvariant(), CreatedAtUtc = now };
        purchase.Confirm(now, purchase.CreatedByUserId);
        return purchase;
    }
    private static CreatorMerchantCampaign Campaign(Guid id, string publicId, Guid creator, Guid merchant, Guid partnership, Guid version, string code, DateTime start, int days, Guid actor, IQrTokenService qrTokens) { var x = new CreatorMerchantCampaign { Id = id, PublicCampaignId = publicId, CreatorId = creator, MerchantId = merchant, MerchantCreatorPartnershipId = partnership, CreatedAtUtc = start }; x.Approve(days, null, version, actor, code, "E2E Active Offer\nSynthetic public description", start); var publicQrId = $"CQR-{code}"; var token = qrTokens.CreateToken(publicQrId, 1); x.QrCode = new() { Id = Guid.NewGuid(), CampaignId = id, PublicQrId = publicQrId, TokenHash = qrTokens.Hash(token), IssuedAtUtc = start, CreatedAtUtc = start }; x.Start(start); x.QrCode.Activate(start, x.ExpiresAtUtc!.Value); return x; }
}
