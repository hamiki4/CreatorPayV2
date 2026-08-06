using System.Security.Cryptography;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Campaigns;
using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPay.Infrastructure.Campaigns;

public sealed class CampaignService(ApplicationDbContext db, IUtcClock clock, IOptions<CampaignOptions> configured) : ICampaignService
{
    private readonly CampaignOptions options = configured.Value;

    public async Task<CampaignDto> RequestAsync(Guid creatorId, RequestCampaignRequest request, CancellationToken ct)
    {
        var partnership = await db.MerchantCreatorPartnerships.SingleOrDefaultAsync(x => x.Id == request.MerchantCreatorPartnershipId && x.CreatorId == creatorId, ct) ?? throw new KeyNotFoundException("Partnership not found.");
        if (partnership.Status != PartnershipStatus.Approved) throw new InvalidOperationException("An approved partnership is required.");
        if (await HasOpenCampaign(partnership.Id, null, ct)) throw new InvalidOperationException("This partnership already has an open campaign.");
        var now = clock.UtcNow;
        var campaign = new CreatorMerchantCampaign { Id = Guid.NewGuid(), PublicCampaignId = $"CMP-{Guid.NewGuid():N}"[..16].ToUpperInvariant(), CreatorId = creatorId, MerchantId = partnership.MerchantId, MerchantCreatorPartnershipId = partnership.Id, RenewedFromCampaignId = request.RenewedFromCampaignId, CreatedAtUtc = now };
        db.Add(campaign); Audit("CampaignRequested", creatorId, partnership.MerchantId, campaign.Id, now); await db.SaveChangesAsync(ct); return Map(campaign);
    }

    public async Task<CampaignApprovalDto> ApproveAsync(Guid merchantId, Guid actor, Guid id, ApproveCampaignRequest request, CancellationToken ct)
    {
        var campaign = await FindMerchant(id, merchantId, ct);
        if (await HasOpenCampaign(campaign.MerchantCreatorPartnershipId, campaign.Id, ct)) throw new InvalidOperationException("This partnership already has an open campaign.");
        var version = await db.CommissionRuleVersions.Include(x => x.Rule).SingleOrDefaultAsync(x => x.Id == request.CommissionRuleVersionId && x.IsActive && x.Rule.IsActive, ct) ?? throw new InvalidOperationException("An active commission rule version is required.");
        if (version.MerchantCommissionRatePercent != 10m || version.CreatorSharePercent != 40m || version.CustomerCashbackSharePercent != 30m || version.PlatformSharePercent != 30m) throw new InvalidOperationException("Campaigns require the versioned 10% four-party commission rule.");
        if (request.MerchantAllowedStartAtUtc?.Kind is not (null or DateTimeKind.Utc)) throw new ArgumentException("Earliest start must be UTC.");
        var businessType = await db.Merchants.Where(x => x.Id == merchantId).Select(x => x.BusinessType).SingleAsync(ct);
        var reuseRule = request.ReuseRule ?? BusinessTypes.SuggestedReuseRule(businessType) ?? throw new ArgumentException("Shopper reuse rule must be selected for this business type.");
        var now = clock.UtcNow; var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); var hash = Hash(raw);
        campaign.Approve(request.DurationDays == 0 ? options.DefaultDurationDays : request.DurationDays, request.MerchantAllowedStartAtUtc, version.Id, actor, RandomCode(), request.Conditions, now, reuseRule);
        campaign.QrCode = new CampaignQrCode { Id = Guid.NewGuid(), CampaignId = campaign.Id, PublicQrId = $"CQR-{Guid.NewGuid():N}"[..16].ToUpperInvariant(), TokenHash = hash, IssuedAtUtc = now, CreatedAtUtc = now };
        db.Add(new CampaignCommissionAssignment { Id = Guid.NewGuid(), CampaignId = campaign.Id, MerchantCreatorPartnershipId = campaign.MerchantCreatorPartnershipId, CommissionRuleId = version.CommissionRuleId, EffectiveFromUtc = now, IsActive = true, CreatedAtUtc = now });
        if (request.EligibleLocationIds is { Length: > 0 })
        {
            var valid = await db.PartnershipLocations.Where(x => x.MerchantCreatorPartnershipId == campaign.MerchantCreatorPartnershipId && request.EligibleLocationIds.Contains(x.MerchantLocationId)).Select(x => x.MerchantLocationId).CountAsync(ct);
            if (valid != request.EligibleLocationIds.Distinct().Count()) throw new ArgumentException("One or more locations are not eligible for this partnership.");
        }
        Audit("CampaignApproved", actor, merchantId, campaign.Id, now); Audit("CampaignQrIssued", actor, merchantId, campaign.QrCode.Id, now); await db.SaveChangesAsync(ct);
        return new(Map(campaign), $"https://creatorpay.example/o/{Uri.EscapeDataString(campaign.CampaignCode)}");
    }

    public async Task<CampaignDto> StartAsync(Guid creatorId, Guid actor, Guid id, CancellationToken ct)
    {
        var campaign = await db.CreatorMerchantCampaigns.Include(x => x.QrCode).Include(x => x.Partnership).SingleOrDefaultAsync(x => x.Id == id && x.CreatorId == creatorId, ct) ?? throw new KeyNotFoundException("Campaign not found.");
        var now = clock.UtcNow;
        if (campaign.Partnership.Status != PartnershipStatus.Approved) throw new InvalidOperationException("Partnership is not active.");
        if (!await IsFundingEligible(campaign.MerchantId, ct)) throw new InvalidOperationException("Promotion is temporarily unavailable because merchant funding eligibility is not met.");
        campaign.Start(now); if (campaign.Status == CampaignStatus.Active) campaign.QrCode!.Activate(now, campaign.ExpiresAtUtc!.Value);
        Audit(campaign.Status == CampaignStatus.Active ? "CampaignStarted" : "CampaignScheduled", actor, campaign.MerchantId, campaign.Id, now); await db.SaveChangesAsync(ct); return Map(campaign);
    }

    public async Task<CampaignDto> RejectAsync(Guid merchantId, Guid actor, Guid id, RejectCampaignRequest request, CancellationToken ct) { if (string.IsNullOrWhiteSpace(request.Reason)) throw new ArgumentException("Reason is required."); var x = await FindMerchant(id, merchantId, ct); x.Reject(clock.UtcNow); Audit("CampaignRejected", actor, merchantId, id, clock.UtcNow); await db.SaveChangesAsync(ct); return Map(x); }
    public async Task<CampaignDto> SuspendAsync(Guid merchantId, Guid actor, Guid id, CancellationToken ct) { var x = await FindMerchant(id, merchantId, ct, true); x.Suspend(clock.UtcNow); Audit("CampaignSuspended", actor, merchantId, id, clock.UtcNow); await db.SaveChangesAsync(ct); return Map(x); }
    public async Task<CampaignDto> CancelAsync(Guid merchantId, Guid actor, Guid id, CancellationToken ct) { var x = await FindMerchant(id, merchantId, ct, true); x.Cancel(clock.UtcNow); Audit("CampaignCancelled", actor, merchantId, id, clock.UtcNow); await db.SaveChangesAsync(ct); return Map(x); }
    public async Task<CampaignDto> RequestRenewalAsync(Guid creatorId, Guid id, RenewalRequest request, CancellationToken ct)
    {
        var old = await db.CreatorMerchantCampaigns.SingleOrDefaultAsync(x => x.Id == id && x.CreatorId == creatorId, ct) ?? throw new KeyNotFoundException("Campaign not found.");
        if (old.Status != CampaignStatus.Expired) throw new InvalidOperationException("Only an expired campaign can be renewed.");
        if (await db.CampaignRenewalRequests.AnyAsync(x => x.ExpiredCampaignId == id && x.Status == CampaignRenewalStatus.Requested, ct)) throw new InvalidOperationException("A renewal request is already open.");
        db.Add(new CampaignRenewalRequest { Id = Guid.NewGuid(), ExpiredCampaignId = id, CreatorId = creatorId, RequestedAtUtc = clock.UtcNow, CreatorNote = request.Note, CreatedAtUtc = clock.UtcNow });
        return await RequestAsync(creatorId, new(old.MerchantCreatorPartnershipId, old.Id, request.Note), ct);
    }
    public async Task<IReadOnlyList<CampaignDto>> GetCreatorCampaignsAsync(Guid creatorId, CancellationToken ct) => await db.CreatorMerchantCampaigns.Include(x => x.QrCode).Where(x => x.CreatorId == creatorId).OrderByDescending(x => x.CreatedAtUtc).Select(x => Map(x)).ToListAsync(ct);
    public async Task<IReadOnlyList<CampaignDto>> GetMerchantCampaignsAsync(Guid merchantId, bool pendingOnly, CancellationToken ct) => await db.CreatorMerchantCampaigns.Include(x => x.QrCode).Where(x => x.MerchantId == merchantId && (!pendingOnly || x.Status == CampaignStatus.PendingApproval)).OrderByDescending(x => x.CreatedAtUtc).Select(x => Map(x)).ToListAsync(ct);
    public async Task<CampaignDto> GetAsync(Guid id, Guid? creatorId, Guid? merchantId, CancellationToken ct) { var q = db.CreatorMerchantCampaigns.Include(x => x.QrCode).Where(x => x.Id == id); if (creatorId.HasValue) q = q.Where(x => x.CreatorId == creatorId); if (merchantId.HasValue) q = q.Where(x => x.MerchantId == merchantId); return Map(await q.SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Campaign not found.")); }
    public async Task<int> ProcessLifecycleAsync(CancellationToken ct)
    {
        var now = clock.UtcNow; var rows = await db.CreatorMerchantCampaigns.Include(x => x.QrCode).Where(x => x.Status == CampaignStatus.Scheduled || x.Status == CampaignStatus.Active).ToListAsync(ct); var count = 0;
        foreach (var x in rows) { if (x.ExpireIfDue(now)) { Audit("CampaignExpired", null, x.MerchantId, x.Id, now); count++; } else if (x.ActivateIfDue(now)) { Audit("CampaignActivated", null, x.MerchantId, x.Id, now); count++; } }
        await db.SaveChangesAsync(ct); return count;
    }
    private async Task<bool> IsFundingEligible(Guid merchantId, CancellationToken ct) => await db.Merchants.AnyAsync(x => x.Id == merchantId && x.Status == MerchantStatus.Active, ct) && (await db.MerchantWallets.AnyAsync(x => x.MerchantId == merchantId && x.CurrencyCode == options.CurrencyCode && x.AvailableBalance >= options.MinimumActivationBalance, ct) || !await db.MerchantTrialCredits.AnyAsync(x => x.MerchantId == merchantId && x.Status != TrialCreditStatus.Active, ct)) && await db.MerchantLocations.AnyAsync(x => x.MerchantId == merchantId && x.IsActive, ct);
    private Task<bool> HasOpenCampaign(Guid partnershipId, Guid? excluding, CancellationToken ct) => db.CreatorMerchantCampaigns.AnyAsync(x => x.MerchantCreatorPartnershipId == partnershipId && x.Id != excluding && (x.Status == CampaignStatus.ApprovedAwaitingStart || x.Status == CampaignStatus.Scheduled || x.Status == CampaignStatus.Active), ct);
    private async Task<CreatorMerchantCampaign> FindMerchant(Guid id, Guid merchantId, CancellationToken ct, bool qr = false) { IQueryable<CreatorMerchantCampaign> q = db.CreatorMerchantCampaigns; if (qr) q = q.Include(x => x.QrCode); return await q.SingleOrDefaultAsync(x => x.Id == id && x.MerchantId == merchantId, ct) ?? throw new KeyNotFoundException("Campaign not found."); }
    private void Audit(string type, Guid? actor, Guid merchant, Guid subject, DateTime now) => db.OperationalAuditEvents.Add(new() { Id = Guid.NewGuid(), EventType = type, ActorUserId = actor, MerchantId = merchant, SubjectId = subject, CorrelationId = Guid.NewGuid().ToString("N"), CreatedAtUtc = now });
    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    private static string RandomCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
    private static CampaignDto Map(CreatorMerchantCampaign x) => new(x.Id, x.PublicCampaignId, x.CreatorId, x.MerchantId, x.MerchantCreatorPartnershipId, x.Status.ToString(), x.DurationDays, x.MerchantAllowedStartAtUtc, x.StartsAtUtc, x.ExpiresAtUtc, x.QrCode?.PublicQrId, x.QrCode?.Status.ToString(), string.IsNullOrEmpty(x.CampaignCode) ? null : x.CampaignCode, x.RenewedFromCampaignId, x.ReuseRule);
}
