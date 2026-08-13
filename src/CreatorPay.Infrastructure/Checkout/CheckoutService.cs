using System.Data;
using System.Security.Cryptography;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Infrastructure.Eligibility;
using CreatorPay.Application.Checkout;
using CreatorPay.Application.Commission;
using CreatorPay.Application.Earnings;
using CreatorPay.Application.Notifications;
using CreatorPay.Application.Operations;
using CreatorPay.Application.Qr;
using CreatorPay.Application.Wallet;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace CreatorPay.Infrastructure.Checkout;

public sealed class CheckoutService(ApplicationDbContext db, IUtcClock clock, IPasswordHasher passwords, PasswordPolicyValidator policy, ICommissionEngine commissions, ICreatorEarningsService earnings, INotificationService notifications, IPhoneNumberNormalizer phoneNumbers, IQrTokenService qrTokens, IOptions<CheckoutOptions> configured, IOptions<WalletOptions> walletConfigured, IOptions<PilotOptions> pilotConfigured, IPhoneOtpService phoneOtp) : ICheckoutService
{
    readonly CheckoutOptions options = configured.Value;
    readonly WalletOptions walletOptions = walletConfigured.Value;
    readonly PilotOptions pilotOptions = pilotConfigured.Value;
    public async Task<CheckoutDto> CreateFromOfferAsync(Guid customer, string key, CreateOfferCheckoutRequest r, CancellationToken ct) { var code = r.OfferCode?.Trim(); if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Offer code is required."); var id = await db.CreatorMerchantCampaigns.Where(x => x.CampaignCode == code || x.PublicCampaignId == code).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Offer not found."); return await CreateAsync(customer, key, new(id), ct); }
    public async Task<Guid> RegisterCustomerAsync(RegisterCustomerRequest r, CancellationToken ct)
    {
        _ = phoneOtp; // retained for constructor compatibility while OTP registration is disabled
        if (r.Password != r.Confirmation) throw new ArgumentException("Passwords do not match.");
        var errors = policy.Validate(r.Password); if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
        var phone = EthiopianMobileNumber.Normalize(r.PhoneNumber); var email = r.Email?.Trim().ToUpperInvariant() ?? "";
        if (r.BirthDate is null) throw new ArgumentException("Birth date is required.");
        if (email.Length > 0 && await db.UserAccounts.AnyAsync(x => x.NormalizedEmail == email, ct)) throw new InvalidOperationException("Email is already registered.");
        if (await db.Customers.AnyAsync(x => x.NormalizedPhoneNumber == phone, ct) || await db.Creators.AnyAsync(x => x.NormalizedPhoneNumber == phone, ct) || await db.Merchants.AnyAsync(x => x.NormalizedPhoneNumber == phone, ct)) throw new InvalidOperationException("Phone number is already registered.");
        var now = clock.UtcNow;
        var customer = new Customer { Id = Guid.NewGuid(), PublicCustomerId = $"CUS-{Guid.NewGuid():N}", DisplayName = r.DisplayName.Trim(), PhoneNumber = phone, NormalizedPhoneNumber = phone, CreatedAtUtc = now };
        var user = new UserAccount { Id = Guid.NewGuid(), Email = r.Email?.Trim() ?? "", NormalizedEmail = email, PhoneNumber = phone, NormalizedPhoneNumber = phone, Role = UserRole.Customer, Status = AccountStatus.Active, CustomerId = customer.Id, BirthDate = r.BirthDate, IsEmailVerified = false, IsPhoneVerified = false, CreatedAtUtc = now };
        user.PasswordHash = passwords.Hash(user, r.Password); db.Add(customer); db.Add(user); db.Add(new CustomerWallet { Id = Guid.NewGuid(), CustomerId = customer.Id, CurrencyCode = options.CurrencyCode, CreatedAtUtc = now });
        Audit("CustomerRegistered", user.Id, customer.Id, null, now);
        await db.SaveChangesAsync(ct); return customer.Id;
    }
    public async Task<CheckoutDto> CreateAsync(Guid customer, string key, CreateCheckoutRequest r, CancellationToken ct) { Key(key); var old = await db.CheckoutSessions.SingleOrDefaultAsync(x => x.CustomerId == customer && x.CreateIdempotencyKey == key, ct); if (old != null) return Map(old); var now = clock.UtcNow; var c = await EligibleCampaign(r.CampaignId, now, ct); if (!await db.Customers.AnyAsync(x => x.Id == customer && x.Status == CustomerStatus.Active, ct)) throw new InvalidOperationException("Customer is not active."); var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var x = new CheckoutSession { Id = Guid.NewGuid(), PublicCheckoutId = $"CHK-{Guid.NewGuid():N}", CustomerId = customer, CampaignId = c.Id, MerchantId = c.MerchantId, CreatorId = c.CreatorId, TokenHash = Hash(raw), ExpiresAtUtc = now.AddMinutes(options.LifetimeMinutes), CreateIdempotencyKey = key, CreatedAtUtc = now }; db.Add(x); Audit("CheckoutCreated", null, customer, x.Id, now); await db.SaveChangesAsync(ct); return Map(x, $"creatorpay:checkout:{x.PublicCheckoutId}:{raw}"); }
    public async Task<CheckoutDto> PresentAsync(Guid merchant, Guid cashier, string key, PresentCheckoutRequest r, CancellationToken ct) { Key(key); if (r.PurchaseAmount <= 0) throw new ArgumentException("Purchase amount must be positive."); var p = r.QrPayload.Split(':'); if (p.Length != 4 || p[0] != "creatorpay" || p[1] != "checkout") throw new ArgumentException("Checkout QR is invalid."); var x = await db.CheckoutSessions.SingleOrDefaultAsync(a => a.PublicCheckoutId == p[2], ct) ?? throw new KeyNotFoundException("Checkout not found."); var now = clock.UtcNow; if (x.ExpiresAtUtc <= now || x.Status != CheckoutSessionStatus.Created) throw new InvalidOperationException("Checkout QR expired or was already used."); if (x.MerchantId != merchant) throw new UnauthorizedAccessException("Checkout belongs to another merchant."); if (Hash(p[3]) != x.TokenHash) throw new UnauthorizedAccessException("Checkout QR authentication failed."); _ = await EligibleCampaign(x.CampaignId, now, ct); if (!await db.CashierLocationAssignments.AnyAsync(a => a.CashierId == cashier && a.MerchantLocationId == r.MerchantLocationId && a.IsActive, ct)) throw new UnauthorizedAccessException("Cashier is not assigned to this location."); var preview = await commissions.PreviewAsync(new(r.PurchaseAmount, merchant, x.CreatorId, null, x.CampaignId, options.CurrencyCode, now), false, ct); if (!await HasRequiredFunding(merchant, preview.Calculation.TotalCommissionAmount, ct)) throw new InvalidOperationException("Business has insufficient funds."); x.PurchaseAmount = r.PurchaseAmount; x.ExpectedCreatorAmount = preview.Calculation.CreatorCommissionAmount; x.ExpectedCashbackAmount = preview.Calculation.CustomerCashbackAmount; x.CashierId = cashier; x.MerchantLocationId = r.MerchantLocationId; x.PresentedAtUtc = now; x.PresentIdempotencyKey = key; x.ExpiresAtUtc = ShopperConfirmationExpires(now); x.Status = CheckoutSessionStatus.AwaitingCustomerApproval; Audit("CheckoutPresented", null, x.CustomerId, x.Id, now); await db.SaveChangesAsync(ct); await Notify(x.CustomerId, NotificationType.CheckoutApprovalRequired, $"checkout:{x.Id}", "Confirm your purchase", $"Did you make this {r.PurchaseAmount:0.00} ETB purchase?", x.Id, ct); return Map(x); }
    public async Task<OfferCheckoutResult> SubmitOfferAsync(Guid merchant, Guid cashier, Guid actor, string key, SubmitOfferCheckoutRequest r, CancellationToken ct)
    {
        Key(key);
        if (pilotOptions.Enabled && r.PurchaseAmount > pilotOptions.MaximumPurchaseAmount) throw new InvalidOperationException("Pilot maximum purchase amount exceeded.");
        if (r.PurchaseAmount <= 0) throw new ArgumentException("Purchase amount must be positive.");
        var phone = EthiopianMobileNumber.Normalize(r.ShopperPhoneNumber);
        var masked = MaskShopperPhone(phone);
        var prior = await db.CheckoutSessions.SingleOrDefaultAsync(x => x.MerchantId == merchant && x.CashierId == cashier && x.PresentIdempotencyKey == key, ct);
        if (prior is not null)
        {
            if (prior.Status == CheckoutSessionStatus.Completed) return new("Success", "completed", "Checkout already completed.", masked, Map(prior));
            return new("AwaitingShopperConfirmation", "awaiting_shopper_confirmation", "Waiting for the Shopper to confirm this purchase.", masked, Map(prior));
        }

        var now = clock.UtcNow;
        var campaign = await ResolveScannedCampaign(merchant, r.QrPayload, now, ct);
        _ = await EligibleCampaign(campaign.Id, now, ct);
        if (!await db.CashierLocationAssignments.AnyAsync(x => x.CashierId == cashier && x.MerchantLocationId == r.MerchantLocationId && x.IsActive, ct) ||
            !await db.MerchantLocations.AnyAsync(x => x.Id == r.MerchantLocationId && x.MerchantId == merchant && x.IsActive, ct))
            throw new UnauthorizedAccessException("Cashier is not assigned to this merchant location.");
        var allowedLocations = db.PartnershipLocations.Where(x => x.MerchantCreatorPartnershipId == campaign.MerchantCreatorPartnershipId && x.IsActive);
        if (await allowedLocations.AnyAsync(ct) && !await allowedLocations.AnyAsync(x => x.MerchantLocationId == r.MerchantLocationId, ct)) throw new UnauthorizedAccessException("Offer is unavailable at this location.");

        var customer = await db.Customers.SingleOrDefaultAsync(x => x.NormalizedPhoneNumber == phone && x.Status == CustomerStatus.Active, ct);
        if (customer is null) return new("ShopperRegistrationRequired", "shopper_not_registered", "Shopper must register with this same phone number before cashback can be issued.", masked, null);
        var preview = await commissions.PreviewAsync(new(r.PurchaseAmount, merchant, campaign.CreatorId, campaign.MerchantCreatorPartnershipId, campaign.Id, options.CurrencyCode, now), false, ct);
        if (pilotOptions.Enabled)
        {
            if (preview.Calculation.TotalCommissionAmount > pilotOptions.MaximumCommissionAmount) throw new InvalidOperationException("Pilot maximum commission amount exceeded.");
            var today = now.Date;
            var merchantSpend = await db.PurchaseTransactions.Where(x => x.TransactionDateUtc >= today && x.MerchantId == merchant).SumAsync(x => (decimal?)x.PurchaseAmount, ct) ?? 0;
            var creatorEarned = await db.CreatorEarnings.Where(x => x.CreatorId == campaign.CreatorId && x.CreatedAtUtc >= today).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            var shopperCashback = await db.CustomerCashbackEntries.Where(x => x.CustomerId == customer.Id && x.CreatedAtUtc >= today && x.EntryType == CustomerCashbackEntryType.Earned).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            if (merchantSpend + r.PurchaseAmount > pilotOptions.DailyMerchantSpendingLimit) throw new InvalidOperationException("Pilot daily Business spending limit exceeded.");
            if (creatorEarned + preview.Calculation.CreatorCommissionAmount > pilotOptions.CreatorEarningLimit) throw new InvalidOperationException("Pilot Creator earning limit exceeded.");
            if (shopperCashback + preview.Calculation.CustomerCashbackAmount > pilotOptions.ShopperCashbackLimit) throw new InvalidOperationException("Pilot Shopper cashback limit exceeded.");
        }
        if (!await HasRequiredFunding(merchant, preview.Calculation.TotalCommissionAmount, ct))
            throw new InvalidOperationException("Business has insufficient funds.");

        var sessionId = Guid.NewGuid();
        var session = new CheckoutSession { Id = sessionId, PublicCheckoutId = $"CHK-{Guid.NewGuid():N}", CustomerId = customer.Id, CampaignId = campaign.Id, MerchantId = merchant, CreatorId = campaign.CreatorId, TokenHash = Hash($"{r.QrPayload}:{sessionId:N}"), ExpiresAtUtc = ShopperConfirmationExpires(now), CreateIdempotencyKey = $"offer:{key}", PresentIdempotencyKey = key, PurchaseAmount = r.PurchaseAmount, ExpectedCreatorAmount = preview.Calculation.CreatorCommissionAmount, ExpectedCashbackAmount = preview.Calculation.CustomerCashbackAmount, CashierId = cashier, MerchantLocationId = r.MerchantLocationId, PresentedAtUtc = now, Status = CheckoutSessionStatus.AwaitingCustomerApproval, CreatedAtUtc = now };
        db.Add(session); Audit("OfferCheckoutSubmitted", actor, customer.Id, session.Id, now); await db.SaveChangesAsync(ct);
        var businessName = await db.Merchants.Where(x => x.Id == merchant).Select(x => x.TradingName).SingleAsync(ct);
        await Notify(customer.Id, NotificationType.CheckoutApprovalRequired, $"checkout:{session.Id}", "Weymela", $"{businessName} — {r.PurchaseAmount:0.00} ETB. Is this your purchase?", session.Id, ct);
        return new("AwaitingShopperConfirmation", "awaiting_shopper_confirmation", "Waiting for the Shopper to confirm this purchase.", masked, Map(session));
    }
    public async Task<CashierQrValidationResult> ValidateOfferAsync(Guid merchant, Guid cashier, Guid location, string payload, CancellationToken ct)
    {
        var now=clock.UtcNow;
        CreatorMerchantCampaign campaign;
        try{campaign=await ResolveScannedCampaign(merchant,payload,now,ct);}catch(Exception x) when(x is ArgumentException or UnauthorizedAccessException or InvalidOperationException){return new(false,"Ineligible","This Creator promotion is not currently eligible at this Business.",null,null);}
        if(!await db.Cashiers.AnyAsync(x=>x.Id==cashier&&x.MerchantId==merchant&&x.IsActive,ct)||
           !await db.CashierLocationAssignments.AnyAsync(x=>x.CashierId==cashier&&x.MerchantLocationId==location&&x.IsActive,ct)||
           !await db.MerchantLocations.AnyAsync(x=>x.Id==location&&x.MerchantId==merchant&&x.IsActive,ct))
            return new(false,"InactiveCashier","This Cashier is not currently active at this Business.",null,null);
        try{_ = await EligibleCampaign(campaign.Id,now,ct);}catch(Exception x) when(x is KeyNotFoundException or InvalidOperationException){return new(false,"Ineligible","This Creator promotion is not currently eligible at this Business.",null,null);}
        var restricted=db.PartnershipLocations.Where(x=>x.MerchantCreatorPartnershipId==campaign.MerchantCreatorPartnershipId&&x.IsActive);
        if(await restricted.AnyAsync(ct)&&!await restricted.AnyAsync(x=>x.MerchantLocationId==location,ct))return new(false,"Ineligible","This Creator promotion is not currently eligible at this Business.",null,null);
        var names=await (from creator in db.Creators.AsNoTracking() join business in db.Merchants.AsNoTracking() on merchant equals business.Id where creator.Id==campaign.CreatorId select new{Creator=creator.DisplayName,Business=business.TradingName}).SingleAsync(ct);
        return new(true,"Eligible","Creator promotion is eligible.",names.Creator,names.Business);
    }
    async Task<CreatorMerchantCampaign> ResolveScannedCampaign(Guid merchant,string payload,DateTime now,CancellationToken ct)
    {
        var value=(payload??string.Empty).Trim();var offer=value.Split(':');
        if(offer.Length==3&&offer[0]=="creatorpay"&&offer[1]=="creator")
        {
            var code=offer[2];
            var creator=await db.Creators.AsNoTracking().SingleOrDefaultAsync(x=>x.CreatorCode==code,ct)??throw new ArgumentException("Creator ID not found.");
            if(creator.Status!=CreatorStatus.Active||!await db.UserAccounts.AnyAsync(x=>x.CreatorId==creator.Id&&x.Role==UserRole.Creator&&x.Status==AccountStatus.Active,ct))throw new InvalidOperationException("Creator account is inactive.");
            var creatorCandidates=await db.CreatorMerchantCampaigns.AsNoTracking().Where(x=>x.MerchantId==merchant&&x.CreatorId==creator.Id).OrderByDescending(x=>x.ExpiresAtUtc).Select(x=>x.Id).ToListAsync(ct);
            foreach(var candidate in creatorCandidates){try{return await EligibleCampaign(candidate,now,ct);}catch(Exception x) when(x is KeyNotFoundException or InvalidOperationException){}}
            throw new InvalidOperationException("No active Creator promotion exists for this Business.");
        }
        if(offer.Length==4&&offer[0]=="creatorpay"&&offer[1]=="offer")
        {
            var campaign=await db.CreatorMerchantCampaigns.Include(x=>x.QrCode).Include(x=>x.Partnership).SingleOrDefaultAsync(x=>x.QrCode!=null&&x.QrCode.PublicQrId==offer[2],ct)??throw new ArgumentException("Offer QR is invalid.");
            if(campaign.QrCode is null||!qrTokens.FixedTimeEquals(offer[3],campaign.QrCode.TokenHash))throw new ArgumentException("Offer QR is invalid.");
            if(campaign.QrCode.Status!=CampaignQrStatus.Active||campaign.QrCode.ExpiresAtUtc<=now)throw new InvalidOperationException("Offer QR is expired or disabled.");
            if(campaign.MerchantId!=merchant)throw new UnauthorizedAccessException("Offer QR belongs to another merchant.");
            return campaign;
        }
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri))throw new ArgumentException("Creator QR is invalid.");
        var path=uri.AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries);var query=System.Web.HttpUtility.ParseQueryString(uri.Query);
        if(path.Length!=2||path[0]!="c"||string.IsNullOrWhiteSpace(query["t"])||query["v"]!="1")throw new ArgumentException("Creator QR is invalid.");
        var creatorQr=await db.CreatorQrCodes.Include(x=>x.Creator).SingleOrDefaultAsync(x=>x.PublicQrId==path[1],ct)??throw new ArgumentException("Creator QR is invalid.");
        if(!qrTokens.FixedTimeEquals(query["t"]!,creatorQr.TokenHash))throw new UnauthorizedAccessException("Creator QR authentication failed.");
        if(!creatorQr.IsActive||creatorQr.RevokedAtUtc.HasValue||creatorQr.Version!=1||creatorQr.Creator.Status!=CreatorStatus.Active)throw new InvalidOperationException("Creator QR is inactive.");
        if(!await db.UserAccounts.AnyAsync(x=>x.CreatorId==creatorQr.CreatorId&&x.Role==UserRole.Creator&&x.Status==AccountStatus.Active,ct))throw new InvalidOperationException("Creator account is inactive.");
        var candidates=await db.CreatorMerchantCampaigns.AsNoTracking().Where(x=>x.MerchantId==merchant&&x.CreatorId==creatorQr.CreatorId).OrderByDescending(x=>x.ExpiresAtUtc).Select(x=>x.Id).ToListAsync(ct);
        foreach(var candidate in candidates)
        {
            try{return await EligibleCampaign(candidate,now,ct);}
            catch(Exception x) when(x is KeyNotFoundException or InvalidOperationException){}
        }
        throw new InvalidOperationException("No active Creator promotion exists for this Business.");
    }
    public async Task<CheckoutDto> ApproveAsync(Guid customer, Guid id, string key, CancellationToken ct) { Key(key); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var x = await db.CheckoutSessions.SingleOrDefaultAsync(a => a.Id == id && a.CustomerId == customer, ct) ?? throw new KeyNotFoundException("Checkout not found."); if (x.Status == CheckoutSessionStatus.Completed) return Map(x); var now = clock.UtcNow; if (x.Status != CheckoutSessionStatus.AwaitingCustomerApproval || x.ExpiresAtUtc <= now) throw new InvalidOperationException("Checkout cannot be approved."); var campaign = await db.CreatorMerchantCampaigns.Include(c => c.Partnership).SingleAsync(c => c.Id == x.CampaignId, ct); var merchant = await db.Merchants.SingleAsync(m => m.Id == x.MerchantId, ct); var creator = await db.Creators.SingleAsync(c => c.Id == x.CreatorId, ct); var customerRow = await db.Customers.SingleAsync(c => c.Id == customer, ct); if (campaign.Status != CampaignStatus.Active || campaign.StartsAtUtc > now || campaign.ExpiresAtUtc <= now || !campaign.Partnership.IsTransactionEligibleAt(now) || merchant.Status != MerchantStatus.Active || creator.Status != CreatorStatus.Active || customerRow.Status != CustomerStatus.Active) throw new InvalidOperationException("Checkout participants are not active."); await EnforceReuseAsync(campaign, x.MerchantLocationId!.Value, customerRow.NormalizedPhoneNumber, now, ct); var calc = await commissions.PreviewAsync(new(x.PurchaseAmount!.Value, x.MerchantId, x.CreatorId, campaign.MerchantCreatorPartnershipId, x.CampaignId, options.CurrencyCode, now), false, ct); var wallet = await db.MerchantWallets.SingleOrDefaultAsync(w => w.MerchantId == x.MerchantId && w.CurrencyCode == options.CurrencyCode, ct); var minimum = await db.PlatformFinancialSettings.Where(s => s.CurrencyCode == options.CurrencyCode).Select(s => (decimal?)s.MinimumBusinessWalletBalance).SingleOrDefaultAsync(ct) ?? 0m; var funded = wallet != null && wallet.AvailableBalance >= minimum && wallet.AvailableBalance >= calc.Calculation.TotalCommissionAmount; if (!funded) throw new InvalidOperationException("Business has insufficient funds."); var snapshot = new CommissionCalculationSnapshot { Id = Guid.NewGuid(), CommissionRuleId = calc.RuleId, CommissionRuleVersionId = calc.RuleVersionId, CurrencyCode = options.CurrencyCode, PurchaseAmount = x.PurchaseAmount.Value, MerchantCommissionRatePercent = calc.Calculation.MerchantCommissionRatePercent, TotalCommissionAmount = calc.Calculation.TotalCommissionAmount, CreatorSharePercent = calc.Calculation.CreatorSharePercent, CreatorCommissionAmount = calc.Calculation.CreatorCommissionAmount, CustomerCashbackSharePercent = calc.Calculation.CustomerCashbackSharePercent, CustomerCashbackAmount = calc.Calculation.CustomerCashbackAmount, PlatformSharePercent = calc.Calculation.PlatformSharePercent, PlatformCommissionAmount = calc.Calculation.PlatformCommissionAmount, RoundingMode = calc.Calculation.RoundingMode, CalculatedAtUtc = now, RuleSourceType = calc.RuleSource, RuleSourceId = calc.RuleSourceId, CreatedAtUtc = now }; var purchase = new PurchaseTransaction { Id = Guid.NewGuid(), PublicTransactionId = $"CP-{Guid.NewGuid():N}", CreatorId = x.CreatorId, CustomerId = customer, CheckoutSessionId = x.Id, MerchantId = x.MerchantId, MerchantLocationId = x.MerchantLocationId!.Value, CashierId = x.CashierId!.Value, MerchantCreatorPartnershipId = campaign.MerchantCreatorPartnershipId, CampaignId = campaign.Id, CampaignQrCodeId = campaign.QrCode?.Id, CampaignCommissionRuleVersionId = campaign.CommissionRuleVersionId, CampaignStartsAtUtc = campaign.StartsAtUtc, CampaignExpiresAtUtc = campaign.ExpiresAtUtc, PurchaseAmount = x.PurchaseAmount.Value, CustomerCashbackAmount = calc.Calculation.CustomerCashbackAmount, CurrencyCode = options.CurrencyCode, CommissionCalculationSnapshotId = snapshot.Id, IdempotencyKey = key, TransactionDateUtc = now, CreatedAtUtc = now, CreatedByUserId = await db.UserAccounts.Where(u => u.CustomerId == customer).Select(u => u.Id).SingleAsync(ct), CorrelationId = key }; purchase.Confirm(now, purchase.CreatedByUserId); var cw = await db.CustomerWallets.SingleAsync(w => w.CustomerId == customer && w.CurrencyCode == options.CurrencyCode, ct); var cb = cw.Credit(calc.Calculation.CustomerCashbackAmount, now); if (cb.Recovered > 0) { var remainingRecovery = cb.Recovered; var recoveries = await db.CustomerRecoveryBalances.Where(r => r.CustomerId == customer && r.Status != CreatorRecoveryStatus.Recovered && r.Status != CreatorRecoveryStatus.Waived).OrderBy(r => r.CreatedAtUtc).ToListAsync(ct); foreach (var recovery in recoveries) { var applied = Math.Min(recovery.OutstandingAmount, remainingRecovery); recovery.OutstandingAmount -= applied; remainingRecovery -= applied; recovery.Status = recovery.OutstandingAmount == 0 ? CreatorRecoveryStatus.Recovered : CreatorRecoveryStatus.PartiallyRecovered; if (remainingRecovery == 0) break; } db.Add(new CustomerCashbackEntry { Id = Guid.NewGuid(), CustomerWalletId = cw.Id, CustomerId = customer, EntryType = CustomerCashbackEntryType.Recovery, Amount = cb.Recovered, BalanceBefore = cb.Before, BalanceAfter = cb.After, CurrencyCode = options.CurrencyCode, PurchaseTransactionId = purchase.Id, IdempotencyKey = $"recovery-offset:{purchase.Id}", CorrelationId = key, CreatedAtUtc = now }); } if (funded) { var merchantBalance = wallet!.Debit(calc.Calculation.TotalCommissionAmount, walletOptions.LowBalanceThreshold, now); db.Add(new MerchantWalletEntry { Id = Guid.NewGuid(), MerchantWalletId = wallet.Id, MerchantId = x.MerchantId, EntryType = MerchantWalletEntryType.CommissionDebit, Amount = calc.Calculation.TotalCommissionAmount, CurrencyCode = options.CurrencyCode, BalanceBefore = merchantBalance.Before, BalanceAfter = merchantBalance.After, RelatedTransactionId = purchase.Id, IdempotencyKey = $"checkout-debit:{purchase.Id}", Description = "Approved four-party checkout", CreatedAtUtc = now, CreatedByUserId = purchase.CreatedByUserId, CreatedBy = purchase.CreatedByUserId.ToString(), CorrelationId = key }); } db.Add(snapshot); db.Add(purchase); db.Add(new CustomerCashbackEntry { Id = Guid.NewGuid(), CustomerWalletId = cw.Id, CustomerId = customer, EntryType = CustomerCashbackEntryType.Earned, Amount = calc.Calculation.CustomerCashbackAmount, BalanceBefore = cb.Before, BalanceAfter = cb.After, CurrencyCode = options.CurrencyCode, PurchaseTransactionId = purchase.Id, IdempotencyKey = $"cashback:{purchase.Id}", CorrelationId = key, CreatedAtUtc = now }); db.Add(new PlatformRevenueEntry { Id = Guid.NewGuid(), PurchaseTransactionId = purchase.Id, Amount = calc.Calculation.PlatformCommissionAmount, CurrencyCode = options.CurrencyCode, IdempotencyKey = $"revenue:{purchase.Id}", CreatedAtUtc = now }); await earnings.RecordConfirmedPurchaseAsync(purchase.Id, x.CreatorId, snapshot.Id, calc.Calculation.CreatorCommissionAmount, options.CurrencyCode, now, key, ct); db.Add(Journal(purchase, calc.Calculation, now, funded, cb.Recovered)); x.Status = CheckoutSessionStatus.Completed; x.CustomerApprovedAtUtc = now; x.CompletedAtUtc = now; x.PurchaseTransactionId = purchase.Id; x.ApprovalIdempotencyKey = key; Audit("FourPartyPurchaseConfirmed", purchase.CreatedByUserId, customer, purchase.Id, now); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await Notify(customer, NotificationType.CustomerCashbackEarned, $"cashback-notify:{purchase.Id}", "Cashback earned", $"{calc.Calculation.CustomerCashbackAmount:0.00} ETB cashback was added.", purchase.Id, ct); return Map(x); }
    public async Task<CheckoutDto> RejectAsync(Guid customer, Guid id, CancellationToken ct) { var x = await db.CheckoutSessions.SingleOrDefaultAsync(a => a.Id == id && a.CustomerId == customer, ct) ?? throw new KeyNotFoundException(); if (x.Status == CheckoutSessionStatus.Rejected) return Map(x); if (x.Status != CheckoutSessionStatus.AwaitingCustomerApproval) throw new InvalidOperationException("Checkout cannot be rejected."); x.Status = CheckoutSessionStatus.Rejected; x.UpdatedAtUtc = clock.UtcNow; await ResolveConfirmationNotification(customer,x.Id,clock.UtcNow,ct); Audit("ShopperRejectedPurchase", null, customer, x.Id, clock.UtcNow); await db.SaveChangesAsync(ct); return Map(x); }
    public async Task<IReadOnlyList<CheckoutDto>> GetCustomerCheckoutsAsync(Guid c, CancellationToken ct) => await (
        from checkout in db.CheckoutSessions.AsNoTracking()
        where checkout.CustomerId == c
        join campaign in db.CreatorMerchantCampaigns.AsNoTracking() on checkout.CampaignId equals campaign.Id
        join merchant in db.Merchants.AsNoTracking() on checkout.MerchantId equals merchant.Id
        join creator in db.Creators.AsNoTracking() on checkout.CreatorId equals creator.Id
        orderby checkout.CreatedAtUtc descending
        select new CheckoutDto(checkout.Id, checkout.PublicCheckoutId, checkout.Status.ToString(), checkout.CustomerId, checkout.CampaignId, checkout.MerchantId, checkout.CreatorId, checkout.PurchaseAmount, checkout.ExpectedCreatorAmount, checkout.ExpectedCashbackAmount, checkout.ExpiresAtUtc, checkout.PurchaseTransactionId, null, merchant.TradingName, creator.DisplayName, campaign.Conditions ?? merchant.TradingName + " Offer", checkout.CreatedAtUtc, checkout.CompletedAtUtc ?? checkout.UpdatedAtUtc)).ToListAsync(ct);
    public async Task<CustomerWalletDto> GetWalletAsync(Guid c, CancellationToken ct) { var w = await db.CustomerWallets.SingleAsync(x => x.CustomerId == c, ct);var now=clock.UtcNow;var schedule=await db.PayoutScheduleVersions.AsNoTracking().Where(x=>x.CurrencyCode==w.CurrencyCode&&x.EffectiveFromUtc<=now).OrderByDescending(x=>x.EffectiveFromUtc).ThenByDescending(x=>x.VersionNumber).FirstOrDefaultAsync(ct);var fallback=await db.PlatformFinancialSettings.AsNoTracking().SingleOrDefaultAsync(x=>x.CurrencyCode==w.CurrencyCode,ct);var next=CreatorPay.Application.Earnings.PayoutSchedule.NextMonthly(now,schedule?.ShopperPayoutDay??fallback?.ShopperPayoutDay??1,TimeSpan.Zero);var last=await db.CustomerPayoutRequests.Where(x=>x.CustomerId==c&&x.Status==CustomerPayoutStatus.Paid).MaxAsync(x=>(DateTime?)x.PaidAtUtc,ct);var cutoff=CreatorPay.Application.Earnings.PayoutSchedule.PreviousMonthlyCutoff(now,schedule?.ShopperCutoffDay??fallback?.ShopperCutoffDay??0,schedule?.ShopperCutoffTime??fallback?.ShopperCutoffTime??TimeSpan.Zero);var periodStart=last.HasValue&&last>cutoff?last.Value:cutoff;var count=await db.PurchaseTransactions.CountAsync(x=>x.CustomerId==c&&x.TransactionDateUtc>=periodStart&&(x.Status==TransactionStatus.Confirmed||x.Status==TransactionStatus.Settled),ct);return new(w.AvailableCashback,w.ReservedCashback,w.PaidLifetime,w.RecoveryBalance,w.CurrencyCode,next,last,count); }
    public async Task<CustomerPayoutDto> RequestPayoutAsync(Guid c, string key, CancellationToken ct) { Key(key); var old = await db.CustomerPayoutRequests.SingleOrDefaultAsync(x => x.CustomerId == c && x.IdempotencyKey == key, ct); if (old != null) return Map(old); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var w = await db.CustomerWallets.SingleAsync(x => x.CustomerId == c, ct); var before = w.AvailableCashback; var moved = w.ReserveFull(options.CashbackPayoutThreshold, clock.UtcNow); var p = new CustomerPayoutRequest { Id = Guid.NewGuid(), PublicPayoutId = $"CPAY-{Guid.NewGuid():N}", CustomerId = c, CustomerWalletId = w.Id, Amount = moved.Before, CurrencyCode = w.CurrencyCode, Status = CustomerPayoutStatus.Requested, RequestedAtUtc = clock.UtcNow, IdempotencyKey = key, CreatedAtUtc = clock.UtcNow }; db.Add(p); db.Add(new CustomerCashbackEntry { Id = Guid.NewGuid(), CustomerWalletId = w.Id, CustomerId = c, EntryType = CustomerCashbackEntryType.PayoutReserved, Amount = p.Amount, BalanceBefore = before, BalanceAfter = 0, CurrencyCode = w.CurrencyCode, CustomerPayoutRequestId = p.Id, IdempotencyKey = $"reserve:{p.Id}", CreatedAtUtc = clock.UtcNow }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(p); }
    public async Task<IReadOnlyList<CustomerPayoutDto>> GetPayoutsAsync(Guid? c, CancellationToken ct) { var q = db.CustomerPayoutRequests.AsQueryable(); if (c.HasValue) q = q.Where(x => x.CustomerId == c); return (await q.OrderByDescending(x => x.RequestedAtUtc).ToListAsync(ct)).Select(Map).ToList(); }
    public async Task<CustomerPayoutDto> MarkPayoutProcessingAsync(Guid id, Guid actor, CancellationToken ct) { var p = await db.CustomerPayoutRequests.SingleAsync(x => x.Id == id, ct); if (p.Status != CustomerPayoutStatus.Requested) throw new InvalidOperationException("Payout is not awaiting processing."); p.Status = CustomerPayoutStatus.Processing; p.ProcessingAtUtc = clock.UtcNow; Audit("CustomerPayoutProcessing", actor, p.CustomerId, p.Id, clock.UtcNow); await db.SaveChangesAsync(ct); return Map(p); }
    public async Task<CustomerPayoutDto> MarkPayoutPaidAsync(Guid id, Guid actor, string key, MarkCustomerPayoutPaidRequest r, CancellationToken ct) { Key(key); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var p = await db.CustomerPayoutRequests.SingleAsync(x => x.Id == id, ct); if (p.Status == CustomerPayoutStatus.Paid) { if (p.PaymentConfirmationIdempotencyKey != key) throw new InvalidOperationException("Payout was already confirmed."); return Map(p); } if (p.Status != CustomerPayoutStatus.Processing) throw new InvalidOperationException("Payout must be processing."); var w = await db.CustomerWallets.SingleAsync(x => x.Id == p.CustomerWalletId, ct); var reservedBefore = w.ReservedCashback; w.MarkPaid(p.Amount, clock.UtcNow); db.Add(new CustomerCashbackEntry { Id = Guid.NewGuid(), CustomerWalletId = w.Id, CustomerId = p.CustomerId, EntryType = CustomerCashbackEntryType.PayoutPaid, Amount = p.Amount, BalanceBefore = reservedBefore, BalanceAfter = w.ReservedCashback, CurrencyCode = w.CurrencyCode, CustomerPayoutRequestId = p.Id, IdempotencyKey = $"paid:{p.Id}:{key}", CorrelationId = key, CreatedAtUtc = clock.UtcNow }); p.Status = CustomerPayoutStatus.Paid; p.PaidAtUtc = r.PaidAtUtc; p.ExternalMethod = r.ExternalMethod; p.ExternalReference = r.ExternalReference; p.PaymentConfirmationIdempotencyKey = key; var j = new FinancialJournal { Id = Guid.NewGuid(), Reference = $"CUSTOMER-PAYOUT-{p.PublicPayoutId}", Description = "Manual customer cashback payout", CreatedAtUtc = clock.UtcNow }; j.Lines.Add(Line(JournalAccount.CustomerCashbackPayable, JournalLineType.Debit, p.Amount, clock.UtcNow)); j.Lines.Add(Line(JournalAccount.PaymentClearing, JournalLineType.Credit, p.Amount, clock.UtcNow)); j.Post(clock.UtcNow); db.Add(j); Audit("CustomerPayoutPaid", actor, p.CustomerId, p.Id, clock.UtcNow); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(p); }
    public async Task<CustomerPayoutDto> MarkPayoutFailedAsync(Guid id, Guid actor, string reason, CancellationToken ct) { var p = await db.CustomerPayoutRequests.SingleAsync(x => x.Id == id, ct); if (p.Status != CustomerPayoutStatus.Processing) throw new InvalidOperationException("Payout must be processing."); var w = await db.CustomerWallets.SingleAsync(x => x.Id == p.CustomerWalletId, ct); var availableBefore = w.AvailableCashback; w.Release(p.Amount, clock.UtcNow); db.Add(new CustomerCashbackEntry { Id = Guid.NewGuid(), CustomerWalletId = w.Id, CustomerId = p.CustomerId, EntryType = CustomerCashbackEntryType.PayoutReleased, Amount = p.Amount, BalanceBefore = availableBefore, BalanceAfter = w.AvailableCashback, CurrencyCode = w.CurrencyCode, CustomerPayoutRequestId = p.Id, IdempotencyKey = $"released:{p.Id}", CreatedAtUtc = clock.UtcNow }); p.Status = CustomerPayoutStatus.Failed; p.FailedAtUtc = clock.UtcNow; p.FailureReason = reason; await db.SaveChangesAsync(ct); return Map(p); }
    public async Task<int> ExpireAsync(CancellationToken ct) { var now = clock.UtcNow; var expired = await db.CheckoutSessions.Where(x => x.ExpiresAtUtc <= now && (x.Status == CheckoutSessionStatus.Created || x.Status == CheckoutSessionStatus.AwaitingCustomerApproval)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, CheckoutSessionStatus.Expired).SetProperty(x => x.UpdatedAtUtc, now), ct); var exhausted = db.MerchantTrialCredits.Where(x => x.Status == TrialCreditStatus.Exhausted).Select(x => x.MerchantId); await db.Merchants.Where(x => exhausted.Contains(x.Id) && x.Status == MerchantStatus.Active).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, MerchantStatus.FundingRestricted).SetProperty(x => x.UpdatedAtUtc, now), ct); return expired; }
    async Task<CreatorMerchantCampaign> EligibleCampaign(Guid id, DateTime now, CancellationToken ct)
    {
        var campaign = await db.CreatorMerchantCampaigns.Include(x => x.Partnership).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Campaign not found.");
        var relationshipEligible = await RewardEligibilityQueries.EligibleRelationships(db, now).AnyAsync(x => x.Id == campaign.MerchantCreatorPartnershipId, ct);
        var hasWalletFunding = await RewardEligibilityQueries.HasRequiredFundingAsync(db, campaign.MerchantId, options.CurrencyCode, 0m, ct);
        var activeLocation = await db.MerchantLocations.AnyAsync(x => x.MerchantId == campaign.MerchantId && x.IsActive, ct);
        var restrictions = db.PartnershipLocations.Where(x => x.MerchantCreatorPartnershipId == campaign.MerchantCreatorPartnershipId && x.IsActive);
        var eligibleLocation = !await restrictions.AnyAsync(ct) || await restrictions.AnyAsync(x => db.MerchantLocations.Any(l => l.Id == x.MerchantLocationId && l.IsActive), ct);
        if (!relationshipEligible) throw new InvalidOperationException("Business, Creator, QR, or advertising relationship is no longer active.");
        if (!hasWalletFunding) throw new InvalidOperationException("Business account requires funding.");
        if (campaign.Status != CampaignStatus.Active || campaign.StartsAtUtc > now || campaign.ExpiresAtUtc <= now || !activeLocation || !eligibleLocation) throw new InvalidOperationException("Advertising QR is not eligible for checkout.");
        return campaign;
    }
    async Task<bool> HasRequiredFunding(Guid merchantId, decimal transactionCommission, CancellationToken ct)
    {
        return await RewardEligibilityQueries.HasRequiredFundingAsync(db, merchantId, options.CurrencyCode, transactionCommission, ct);
    }
    async Task EnforceReuseAsync(CreatorMerchantCampaign campaign, Guid locationId, string normalizedPhone, DateTime now, CancellationToken ct)
    {
        if (campaign.ReuseRule == OfferReuseRule.Unlimited) return;
        var zoneId = await db.MerchantLocations.Where(x => x.Id == locationId).Select(x => x.TimeZoneId).SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var startUtc = OfferReuseWindow.StartUtc(campaign.ReuseRule, now, zone);
        var confirmed = db.PurchaseTransactions
            .Where(x => x.CampaignId == campaign.Id && x.CustomerId.HasValue && (x.Status == TransactionStatus.Confirmed || x.Status == TransactionStatus.Settled || x.Status == TransactionStatus.Disputed || x.Status == TransactionStatus.PartiallyReversed))
            .Join(db.Customers, purchase => purchase.CustomerId!.Value, shopper => shopper.Id, (purchase, shopper) => new { purchase.TransactionDateUtc, shopper.NormalizedPhoneNumber })
            .Where(x => x.NormalizedPhoneNumber == normalizedPhone);
        if (startUtc.HasValue) confirmed = confirmed.Where(x => x.TransactionDateUtc >= startUtc.Value);
        if (!await confirmed.AnyAsync(ct)) return;

        var shopperId = await db.Customers.Where(x => x.NormalizedPhoneNumber == normalizedPhone).Select(x => x.Id).SingleAsync(ct);
        var checkout = await db.CheckoutSessions.SingleAsync(x =>
            x.CampaignId == campaign.Id &&
            x.CustomerId == shopperId &&
            x.MerchantLocationId == locationId &&
            x.Status == CheckoutSessionStatus.AwaitingCustomerApproval,
            ct);
        var maskedPhone = phoneNumbers.Mask(normalizedPhone);
        var approval = await db.RepeatUseApprovalRequests.SingleOrDefaultAsync(x =>
            x.QrReference == checkout.PublicCheckoutId &&
            x.MerchantId == checkout.MerchantId &&
            x.MerchantLocationId == locationId &&
            x.CreatorId == checkout.CreatorId &&
            x.MerchantCreatorPartnershipId == campaign.MerchantCreatorPartnershipId &&
            x.CashierId == checkout.CashierId &&
            x.MaskedPhoneNumber == maskedPhone,
            ct);
        if (approval is null || approval.Status != RepeatUseApprovalStatus.SupervisorApproved ||
            approval.ExpiresAtUtc <= now || string.IsNullOrWhiteSpace(approval.Reason) ||
            approval.SupervisorUserId is null || approval.SupervisorDecisionAtUtc is null ||
            approval.RelatedPurchaseTransactionId is not null ||
            approval.RequiresCustomerConfirmation && approval.CustomerConfirmedAtUtc is null)
            throw new InvalidOperationException("This Shopper has already used this Offer within its allowed reuse period.");

        var approver = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == approval.SupervisorUserId, ct);
        if (approver is null || approver.Status != AccountStatus.Active || approver.MerchantId != checkout.MerchantId ||
            approver.Role is not (UserRole.Supervisor or UserRole.MerchantAdmin) || approver.CashierId == checkout.CashierId)
            throw new InvalidOperationException("The repeat-use approval is not authorized.");

        approval.Status = RepeatUseApprovalStatus.Approved;
        approval.FinalizedAtUtc = now;
        db.RepeatUseApprovalHistories.Add(new RepeatUseApprovalHistory
        {
            Id = Guid.NewGuid(),
            RepeatUseApprovalRequestId = approval.Id,
            PreviousStatus = RepeatUseApprovalStatus.SupervisorApproved,
            NewStatus = RepeatUseApprovalStatus.Approved,
            ActorUserId = approval.SupervisorUserId,
            Action = "AppliedToCheckout",
            Reason = approval.Reason,
            OccurredAtUtc = now,
            CreatedAtUtc = now
        });
    }
    async Task Notify(Guid customer, NotificationType type, string key, string title, string body, Guid entity, CancellationToken ct) { if(type==NotificationType.CustomerCashbackEarned){var checkout=await db.CheckoutSessions.Where(x=>x.PurchaseTransactionId==entity).Select(x=>(Guid?)x.Id).SingleOrDefaultAsync(ct);if(checkout.HasValue){await ResolveConfirmationNotification(customer,checkout.Value,clock.UtcNow,ct);await db.SaveChangesAsync(ct);}}var user = await db.UserAccounts.Where(x => x.CustomerId == customer).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct); if (user.HasValue) await notifications.CreateAsync(new(type, key, new Dictionary<string, string> { { "Title", title }, { "Body", body }, { "TargetPath", "/?view=confirmations" } }, [new(user, NotificationRecipientType.User, NotificationChannel.InApp, null, null)], NotificationPriority.High, key, "Checkout", entity.ToString()), ct); }
    DateTime ShopperConfirmationExpires(DateTime now) => now.AddMinutes(Math.Max(1, options.ShopperConfirmationLifetimeMinutes));
    void Audit(string type, Guid? actor, Guid customer, Guid? subject, DateTime now) => db.OperationalAuditEvents.Add(new() { Id = Guid.NewGuid(), EventType = type, ActorUserId = actor, SubjectId = subject, MetadataJson = $"{{\"customerId\":\"{customer}\"}}", CorrelationId = Guid.NewGuid().ToString("N"), CreatedAtUtc = now });
    static FinancialJournal Journal(PurchaseTransaction p, CreatorPay.Domain.Services.CommissionCalculation c, DateTime now, bool merchantFunded, decimal customerRecoveryOffset) { var j = new FinancialJournal { Id = Guid.NewGuid(), Reference = $"FOUR-PARTY-{p.PublicTransactionId}", Description = "Approved four-party checkout", RelatedTransactionId = p.Id, CreatedAtUtc = now }; j.Lines.Add(Line(merchantFunded ? JournalAccount.MerchantWalletLiability : JournalAccount.PlatformTrialCreditExpense, JournalLineType.Debit, c.TotalCommissionAmount, now)); j.Lines.Add(Line(JournalAccount.CreatorPayable, JournalLineType.Credit, c.CreatorCommissionAmount, now)); if (c.CustomerCashbackAmount - customerRecoveryOffset > 0) j.Lines.Add(Line(JournalAccount.CustomerCashbackPayable, JournalLineType.Credit, c.CustomerCashbackAmount - customerRecoveryOffset, now)); if (customerRecoveryOffset > 0) j.Lines.Add(Line(JournalAccount.CustomerRecoveryReceivable, JournalLineType.Credit, customerRecoveryOffset, now)); j.Lines.Add(Line(JournalAccount.PlatformCommissionRevenue, JournalLineType.Credit, c.PlatformCommissionAmount, now)); j.Post(now); return j; }
    static FinancialJournalLine Line(JournalAccount a, JournalLineType t, decimal amount, DateTime now) => new() { Id = Guid.NewGuid(), Account = a, Type = t, Amount = amount, CurrencyCode = "ETB", Description = a.ToString(), CreatedAtUtc = now }; static string Hash(string x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x))); static void Key(string x) { if (string.IsNullOrWhiteSpace(x) || x.Length > 200) throw new ArgumentException("Idempotency-Key is required."); }
    string MaskShopperPhone(string normalizedPhone) => normalizedPhone.StartsWith("+251", StringComparison.Ordinal) && normalizedPhone.Length == 13 ? $"09*****{normalizedPhone[^3..]}" : phoneNumbers.Mask(normalizedPhone);
    async Task ResolveConfirmationNotification(Guid customer,Guid checkout,DateTime now,CancellationToken ct){var user=await db.UserAccounts.Where(x=>x.CustomerId==customer).Select(x=>x.Id).SingleAsync(ct);var rows=await db.NotificationRecipients.Include(x=>x.Notification).Where(x=>x.UserAccountId==user&&x.Channel==NotificationChannel.InApp&&x.Notification.RelatedEntityId==checkout.ToString()&&!x.ReadAtUtc.HasValue).ToListAsync(ct);foreach(var row in rows){row.ReadAtUtc=now;row.Status=NotificationRecipientStatus.Read;db.NotificationAuditEvents.Add(new(){Id=Guid.NewGuid(),NotificationId=row.NotificationId,ActorUserId=user,EventType="PurchaseConfirmationResolved",CreatedAtUtc=now});}}
    static CheckoutDto Map(CheckoutSession x, string? qr = null) => new(x.Id, x.PublicCheckoutId, x.Status.ToString(), x.CustomerId, x.CampaignId, x.MerchantId, x.CreatorId, x.PurchaseAmount, x.ExpectedCreatorAmount, x.ExpectedCashbackAmount, x.ExpiresAtUtc, x.PurchaseTransactionId, qr,CreatedAtUtc:x.CreatedAtUtc,ResolvedAtUtc:x.CompletedAtUtc??x.UpdatedAtUtc); static CustomerPayoutDto Map(CustomerPayoutRequest p) => new(p.Id, p.PublicPayoutId, p.Amount, p.CurrencyCode, p.Status.ToString(), p.RequestedAtUtc, p.PaidAtUtc, p.ExternalReference, p.CustomerId);
}
