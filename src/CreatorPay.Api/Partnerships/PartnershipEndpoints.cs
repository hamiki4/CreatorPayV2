using System.Security.Cryptography;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Campaigns;
using CreatorPay.Application.Commission;
using CreatorPay.Application.Discovery;
using CreatorPay.Application.Merchants;
using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CreatorPay.Infrastructure.Eligibility;

namespace CreatorPay.Api.Partnerships;

public sealed record CreatePartnershipRequest(Guid MerchantId, string? IntroductoryMessage, DateTime? RequestedStartDateUtc);
public sealed record MerchantAddCreatorRequest(Guid CreatorId, bool ConfirmApproval, string? IntroductoryMessage, DateTime? StartDateUtc, DateTime? EndDateUtc, Guid[]? LocationIds);
public sealed record MerchantInviteCreatorRequest(Guid CreatorId, string? IntroductoryMessage);
public sealed record PartnershipReasonRequest(string? Reason);
public sealed record PartnershipDatesRequest(DateTime? StartDateUtc, DateTime? EndDateUtc);
public sealed record PartnershipLocationsRequest(Guid[] LocationIds);
public sealed record SubmitPromotionVideoRequest(string VideoUrl);
public sealed record PromotionVideoActionRequest(string? Reason);
public sealed record PartnershipListItem(Guid Id, Guid MerchantId, string MerchantName, Guid CreatorId, string CreatorName, PartnershipStatus Status, DateTime RequestedAtUtc, DateTime? StartDateUtc, DateTime? EndDateUtc, string? IntroductoryMessage, IReadOnlyList<LocationItem> Locations, string InitiatedBy = "Unknown", DateTime? ActivatedAtUtc = null, DateTime? ExpiresAtUtc = null, bool PromotionActive = false, string RelationshipState = "Pending", bool ActivationRequired = false, string? CreatorSocialPlatform = null, string? CreatorSocialProfileUrl = null, string? CreatorCity = null, long? CreatorFollowerCount = null, string? CreatorProfileImageUrl = null, string? CreatorPhoneNumber = null, PromotionVideoSummaryDto? PromotionVideo = null, string? CreatorPublicId = null);
public sealed record LocationItem(Guid Id, string Name, bool IsActive);

public static class PartnershipEndpoints
{
    public static IEndpointRouteBuilder MapPartnershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var creator = endpoints.MapGroup("/api/v1/creator").WithTags("Creator partnerships").RequireAuthorization("CreatorOnly");
        creator.MapGet("/merchants/search", (string? q, ICurrentUserService user, ApplicationDbContext db, IOptions<CampaignOptions> campaignOptions, CancellationToken ct) => SearchMerchants(q, user, db, campaignOptions, ct));
        creator.MapPost("/partnerships/requests", (CreatePartnershipRequest request, ICurrentUserService user, ApplicationDbContext db, INotificationService notifications, HttpContext http, IOptions<CampaignOptions> campaignOptions, CancellationToken ct) => RequestPartnership(request, user, db, notifications, http, campaignOptions, ct));
        creator.MapGet("/partnerships", (string? status, ICurrentUserService u, ApplicationDbContext db, IOptions<CampaignOptions> campaignOptions, CancellationToken ct) => CreatorPartnerships(status, u, db, campaignOptions, ct));
        creator.MapGet("/partnerships/{id:guid}", CreatorPartnership);
        creator.MapPost("/partnerships/{id:guid}/withdraw", (Guid id, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct) => CreatorRevoke(id, u, db, h, ct, "PartnershipWithdrawn"));
        creator.MapPost("/partnerships/{id:guid}/stop-promoting", (Guid id, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct) => CreatorRevoke(id, u, db, h, ct, "CreatorStoppedPromoting"));
        creator.MapPost("/partnerships/{id:guid}/accept-invitation", (Guid id, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => CreatorInvitationDecision(id, true, u, db, commissions, campaignOptions, notifications, h, ct));
        creator.MapPost("/partnerships/{id:guid}/decline-invitation", (Guid id, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => CreatorInvitationDecision(id, false, u, db, commissions, campaignOptions, notifications, h, ct));
        creator.MapPost("/partnerships/{id:guid}/promotion-video", (Guid id, SubmitPromotionVideoRequest request, ICurrentUserService u, ApplicationDbContext db, INotificationService notifications, ITikTokVideoUrlResolver videoUrls, IOptions<CampaignOptions> campaignOptions, HttpContext h, CancellationToken ct) => SubmitPromotionVideo(id, request, u, db, notifications, videoUrls, campaignOptions, h, ct));
        creator.MapPost("/partnerships/{id:guid}/go-live", (Guid id, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, CancellationToken ct) => GoLive(id, u, db, commissions, campaignOptions, ct));
        creator.MapGet("/promotion-videos", (ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => CreatorPromotionVideos(u, db, ct));

        var merchant = endpoints.MapGroup("/api/v1/merchant").WithTags("Merchant partnerships").RequireAuthorization("MerchantAdminOnly");
        merchant.MapGet("/creators/search", SearchCreators);
        merchant.MapGet("/partnerships", (string? status, ICurrentUserService u, ApplicationDbContext db, IOptions<CampaignOptions> campaignOptions, CancellationToken ct) => MerchantPartnerships(status, u, db, campaignOptions, ct));
        merchant.MapGet("/promotion-videos", (string? status, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => MerchantPromotionVideos(status, u, db, ct));
        merchant.MapGet("/dashboard-metrics", MerchantDashboardMetrics);
        merchant.MapGet("/partnerships/{id:guid}", MerchantPartnership);
        merchant.MapPost("/partnerships", MerchantAdd);
        merchant.MapPost("/partnerships/invitations", MerchantInvite);
        merchant.MapPost("/partnerships/{id:guid}/approve", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Approved, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/reject", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Rejected, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/suspend", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Suspended, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/activate", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Approved, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/reconcile-readiness", (Guid id, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, CancellationToken ct) => ReconcileReadiness(id, u, db, commissions, campaignOptions, ct));
        merchant.MapPost("/partnerships/{id:guid}/reactivate", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Approved, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct, reactivationOnly: true));
        merchant.MapPost("/partnerships/{id:guid}/revoke", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Revoked, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/block", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Blocked, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPut("/partnerships/{id:guid}/locations", SetLocations);
        merchant.MapPut("/partnerships/{id:guid}/dates", SetDates);
        merchant.MapPost("/promotion-videos/{id:guid}/approve", (Guid id, PromotionVideoActionRequest request, ICurrentUserService u, ApplicationDbContext db, INotificationService notifications, HttpContext h, CancellationToken ct) => ReviewPromotionVideo(id, true, request, u, db, notifications, h, ct));
        merchant.MapPost("/promotion-videos/{id:guid}/reject", (Guid id, PromotionVideoActionRequest request, ICurrentUserService u, ApplicationDbContext db, INotificationService notifications, HttpContext h, CancellationToken ct) => ReviewPromotionVideo(id, false, request, u, db, notifications, h, ct));

        var admin = endpoints.MapGroup("/api/v1/admin/partnerships").WithTags("Partnership support").RequireAuthorization("AdminOperationsOnly");
        admin.MapGet("/", async (ApplicationDbContext db, CancellationToken ct) => Results.Ok((await Query(db).ToListAsync(ct)).Select(Item)));
        admin.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db, CancellationToken ct) => await Query(db).FirstOrDefaultAsync(x => x.Id == id, ct) is { } p ? Results.Ok(Item(p)) : NotFound());
        return endpoints;
    }

    private static async Task<IResult> SearchMerchants(string? q, ICurrentUserService user, ApplicationDbContext db, IOptions<CampaignOptions> campaignOptions, CancellationToken ct)
    {
        var creatorId = user.CreatorId!.Value; q = q?.Trim();
        var blocked = db.MerchantCreatorPartnerships.Where(x => x.CreatorId == creatorId && x.Status == PartnershipStatus.Blocked).Select(x => x.MerchantId);
        var current = db.MerchantCreatorPartnerships.Where(x => x.CreatorId == creatorId && (x.Status == PartnershipStatus.Pending || x.Status == PartnershipStatus.Approved)).Select(x => x.MerchantId);
        var query = db.Merchants.AsNoTracking().Where(x => x.Status == MerchantStatus.Active && db.UserAccounts.Any(a => a.MerchantId == x.Id && a.Role == UserRole.MerchantAdmin && a.Status == AccountStatus.Active) && !blocked.Contains(x.Id) && !current.Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => EF.Functions.ILike(x.TradingName, $"%{q}%") || EF.Functions.ILike(x.LegalBusinessName, $"%{q}%") || EF.Functions.ILike(x.PublicMerchantId, $"%{q}%") || EF.Functions.ILike(x.City, $"%{q}%") || EF.Functions.ILike(x.BusinessType, $"%{q}%"));
        var rows = await query.OrderBy(x => x.TradingName).Take(50).Select(x => new { x.Id, x.PublicMerchantId, x.TradingName, x.LegalBusinessName, x.City, x.BusinessType }).ToListAsync(ct);
        var eligible = await BusinessAdvertisingEligibility.EligibleMerchantIdsAsync(db, rows.Select(x => x.Id), campaignOptions.Value.CurrencyCode, ct);
        return Results.Ok(rows.Where(x => eligible.Contains(x.Id)).ToList());
    }

    private static async Task<IResult> RequestPartnership(CreatePartnershipRequest request, ICurrentUserService user, ApplicationDbContext db, INotificationService notifications, HttpContext http, IOptions<CampaignOptions> campaignOptions, CancellationToken ct)
    {
        var now = DateTime.UtcNow; var creatorId = user.CreatorId!.Value;
        var creator = await db.Creators.FindAsync([creatorId], ct); var merchant = await db.Merchants.FindAsync([request.MerchantId], ct);
        if (creator?.Status != CreatorStatus.Active) return Problem(403, "Creator is not active.");
        if (merchant?.Status != MerchantStatus.Active) return Problem(400, "Merchant is not eligible to receive requests.");
        var eligibility = await BusinessAdvertisingEligibility.EvaluateAsync(db, request.MerchantId, campaignOptions.Value.CurrencyCode, ct);
        if (!eligibility.Eligible) return Problem(409, $"Advertising is restricted until the available wallet balance reaches {eligibility.Minimum:0.00} {campaignOptions.Value.CurrencyCode}.");
        var existing = await db.MerchantCreatorPartnerships.Where(x => x.CreatorId == creatorId && x.MerchantId == request.MerchantId).Select(x => x.Status).ToListAsync(ct);
        if (existing.Contains(PartnershipStatus.Blocked)) return Problem(409, "This relationship is blocked.");
        if (existing.Any(x => x is PartnershipStatus.Pending or PartnershipStatus.Approved)) return Problem(409, "A pending or active relationship with this merchant already exists.");
        var p = new MerchantCreatorPartnership { Id = Guid.NewGuid(), CreatorId = creatorId, MerchantId = request.MerchantId, RequestedAtUtc = now, RequestedByUserId = user.UserAccountId, IntroductoryMessage = request.IntroductoryMessage?.Trim(), CreatedAtUtc = now, CreatedBy = user.UserAccountId.ToString() };
        if (request.RequestedStartDateUtc.HasValue) p.SetDates(request.RequestedStartDateUtc, null, now, user.UserAccountId!.Value);
        db.Add(p); Audit(db, p, user.UserAccountId!.Value, PartnershipStatus.Pending, PartnershipStatus.Pending, "PartnershipRequested", null, http, now);
        await NotifyMerchant(notifications, db, p.MerchantId, NotificationType.PartnershipRequested, $"partnership:{p.Id}:requested", "New Creator request", $"{creator.DisplayName} requested permission to promote your Business.", "/?view=requests", http.TraceIdentifier, p.Id, ct); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/creator/partnerships/{p.Id}", Item(p, merchant, creator));
    }

    private static async Task<IResult> CreatorPartnerships(string? status, ICurrentUserService u, ApplicationDbContext db, IOptions<CampaignOptions> campaignOptions, CancellationToken ct)
    { var query = Query(db).Where(x => x.CreatorId == u.CreatorId); if (Enum.TryParse<PartnershipStatus>(status, true, out var s)) query = query.Where(x => x.Status == s); return Results.Ok(await ItemsWithInitiator(query, db, campaignOptions.Value.CurrencyCode, ct)); }
    private static async Task<IResult> CreatorPartnership(Guid id, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => await Query(db).FirstOrDefaultAsync(x => x.Id == id && x.CreatorId == u.CreatorId, ct) is { } p ? Results.Ok(Item(p)) : NotFound();

    private static async Task<IResult> SearchCreators(string? q, string? sort, ICurrentUserService u, ApplicationDbContext db, IOptions<CampaignOptions> campaignOptions, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var eligibility = await BusinessAdvertisingEligibility.EvaluateAsync(db, u.MerchantId!.Value, campaignOptions.Value.CurrencyCode, ct);
        if (!eligibility.Eligible) return Problem(409, $"Advertising is restricted until the available wallet balance reaches {eligibility.Minimum:0.00} {campaignOptions.Value.CurrencyCode}.");
        q = q?.Trim();
        var current = db.MerchantCreatorPartnerships.Where(x => x.MerchantId == u.MerchantId && (x.Status == PartnershipStatus.Pending || x.Status == PartnershipStatus.Approved)).Select(x => x.CreatorId);
        var query = db.Creators.AsNoTracking().Where(x => x.Status == CreatorStatus.Active && db.UserAccounts.Any(account => account.CreatorId == x.Id && account.Role == UserRole.Creator && account.Status == AccountStatus.Active && (account.LockoutEndUtc == null || account.LockoutEndUtc <= now)) && !current.Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => EF.Functions.ILike(x.DisplayName, $"%{q}%") || EF.Functions.ILike(x.PublicCreatorId, $"%{q}%") || x.SocialProfiles.Any(s => EF.Functions.ILike(s.Handle, $"%{q}%")));
        var rankByFollowers = string.Equals(sort, "followers", StringComparison.OrdinalIgnoreCase);
        if (rankByFollowers) query = query.Where(x => x.SocialProfiles.Any());
        var ordered = rankByFollowers
            ? query.OrderByDescending(x => x.SocialProfiles.OrderByDescending(s => s.IsPrimary).Select(s => (long?)s.FollowerCount).FirstOrDefault() ?? -1L).ThenBy(x => x.DisplayName)
            : query.OrderBy(x => x.DisplayName);
        var rows = await ordered.Take(50).Select(x => new
        {
            x.Id,
            x.PublicCreatorId,
            x.DisplayName,
            x.City,
            x.PhoneNumber,
            x.Biography,
            x.ContentCategories,
            x.ProfileImageFileName,
            SocialPlatform = x.SocialProfiles.OrderByDescending(s => s.IsPrimary).Select(s => (SocialPlatform?)s.Platform).FirstOrDefault(),
            SocialProfileUrl = x.SocialProfiles.OrderByDescending(s => s.IsPrimary).Select(s => s.ProfileUrl).FirstOrDefault(),
            FollowerCount = x.SocialProfiles.OrderByDescending(s => s.IsPrimary).Select(s => (long?)s.FollowerCount).FirstOrDefault()
        }).ToListAsync(ct);
        return Results.Ok(rows.Select(x => new
        {
            x.Id,
            x.PublicCreatorId,
            x.DisplayName,
            x.City,
            x.Biography,
            x.ContentCategories,
            x.SocialPlatform,
            x.SocialProfileUrl,
            x.FollowerCount,
            PhoneNumber = x.PhoneNumber,
            ProfileImageUrl = x.ProfileImageFileName is null ? null : $"/api/v1/discovery/creators/{Uri.EscapeDataString(x.PublicCreatorId)}/photo?v={Uri.EscapeDataString(x.ProfileImageFileName)}"
        }));
    }
    private static async Task<IResult> MerchantPartnerships(string? status, ICurrentUserService u, ApplicationDbContext db, IOptions<CampaignOptions> campaignOptions, CancellationToken ct)
    { var query = Query(db).Where(x => x.MerchantId == u.MerchantId); if (Enum.TryParse<PartnershipStatus>(status, true, out var s)) query = query.Where(x => x.Status == s); return Results.Ok(await ItemsWithInitiator(query, db, campaignOptions.Value.CurrencyCode, ct)); }
    private static async Task<IResult> MerchantDashboardMetrics(ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) { var sales = await db.PurchaseTransactions.CountAsync(x => x.MerchantId == u.MerchantId && (x.Status == TransactionStatus.Confirmed || x.Status == TransactionStatus.Settled), ct); return Results.Ok(new { confirmedSales = sales, period = "All Time" }); }
    private static async Task<IResult> MerchantPartnership(Guid id, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => await Query(db).FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct) is { } p ? Results.Ok(Item(p)) : NotFound();

    private static async Task<IResult> MerchantInvite(MerchantInviteCreatorRequest request, ICurrentUserService u, ApplicationDbContext db, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct)
    {
        var eligibility = await BusinessAdvertisingEligibility.EvaluateAsync(db, u.MerchantId!.Value, campaignOptions.Value.CurrencyCode, ct);
        if (!eligibility.Eligible) return Problem(409, $"Advertising is restricted until the available wallet balance reaches {eligibility.Minimum:0.00} {campaignOptions.Value.CurrencyCode}.");
        var creator = await db.Creators.FindAsync([request.CreatorId], ct); if (creator?.Status != CreatorStatus.Active) return Problem(400, "Only an active platform-approved creator can be invited.");
        var existing = await db.MerchantCreatorPartnerships.Where(x => x.MerchantId == u.MerchantId && x.CreatorId == request.CreatorId).Select(x => x.Status).ToListAsync(ct);
        if (existing.Contains(PartnershipStatus.Blocked)) return Problem(409, "This relationship is blocked.");
        if (existing.Any(x => x is PartnershipStatus.Pending or PartnershipStatus.Approved)) return Problem(409, "A pending or active relationship with this creator already exists.");
        var now = DateTime.UtcNow; var p = new MerchantCreatorPartnership { Id = Guid.NewGuid(), MerchantId = u.MerchantId!.Value, CreatorId = request.CreatorId, RequestedAtUtc = now, RequestedByUserId = u.UserAccountId, IntroductoryMessage = request.IntroductoryMessage?.Trim(), CreatedAtUtc = now, CreatedBy = u.UserAccountId.ToString() };
        var merchant = await db.Merchants.FindAsync([u.MerchantId.Value], ct) ?? new();
        db.Add(p); Audit(db, p, u.UserAccountId!.Value, PartnershipStatus.Pending, PartnershipStatus.Pending, "CreatorAdvertisingInvitationSent", null, h, now);
        await NotifyCreator(notifications, db, p.CreatorId, NotificationType.PartnershipRequested, $"partnership:{p.Id}:invited", "New Business invitation", $"{merchant.TradingName} invited you to promote their Business.", "/?view=find", h.TraceIdentifier, p.Id, ct); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/merchant/partnerships/{p.Id}", Item(p, merchant, creator) with { InitiatedBy = "Business" });
    }

    private static async Task<IResult> CreatorInvitationDecision(Guid id, bool accept, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct)
    {
        var p = await db.MerchantCreatorPartnerships.Include(x => x.Merchant).Include(x => x.Creator).FirstOrDefaultAsync(x => x.Id == id && x.CreatorId == u.CreatorId, ct); if (p is null) return NotFound();
        if (p.Status != PartnershipStatus.Pending) return Problem(409, "Only a pending invitation can be decided.");
        var initiatedByBusiness = await db.UserAccounts.AnyAsync(x => x.Id == p.RequestedByUserId && x.MerchantId == p.MerchantId, ct); if (!initiatedByBusiness) return Problem(409, "This request was initiated by the creator.");
        var now = DateTime.UtcNow; var old = p.Status;
        if (accept)
        {
            var eligibility = await BusinessAdvertisingEligibility.EvaluateAsync(db, p.MerchantId, campaignOptions.Value.CurrencyCode, ct);
            if (!eligibility.Eligible) return Problem(409, $"Advertising is restricted until the available wallet balance reaches {eligibility.Minimum:0.00} {campaignOptions.Value.CurrencyCode}.");
            p.Approve(now, u.UserAccountId!.Value, p.StartDateUtc, p.EndDateUtc);
        }
        else p.Reject(now, u.UserAccountId!.Value, "Declined by creator");
        Audit(db, p, u.UserAccountId.Value, old, p.Status, accept ? "CreatorAdvertisingInvitationAccepted" : "CreatorAdvertisingInvitationDeclined", null, h, now);
        await NotifyMerchant(notifications, db, p.MerchantId, accept ? NotificationType.PartnershipApproved : NotificationType.PartnershipRejected, $"partnership:{p.Id}:creator:{p.Status}", accept ? "Creator accepted your invitation" : "Creator declined your invitation", $"{p.Creator.DisplayName} has {(accept ? "accepted" : "declined")} your invitation.", "/?view=requests", h.TraceIdentifier, p.Id, ct);
        await db.SaveChangesAsync(ct); return Results.Ok(Item(p) with { InitiatedBy = "Business", RelationshipState = accept ? "AwaitingVideo" : "Declined", ActivationRequired = accept });
    }

    private static async Task<IResult> MerchantAdd(MerchantAddCreatorRequest request, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, HttpContext h, CancellationToken ct)
    {
        var eligibility = await BusinessAdvertisingEligibility.EvaluateAsync(db, u.MerchantId!.Value, campaignOptions.Value.CurrencyCode, ct);
        if (!eligibility.Eligible) return Problem(409, $"Advertising is restricted until the available wallet balance reaches {eligibility.Minimum:0.00} {campaignOptions.Value.CurrencyCode}.");
        if (!request.ConfirmApproval) return Problem(400, "Explicit approval confirmation is required.");
        var creator = await db.Creators.FindAsync([request.CreatorId], ct); if (creator?.Status != CreatorStatus.Active) return Problem(400, "Only an active platform-approved creator can be added.");
        var existing = await db.MerchantCreatorPartnerships.Where(x => x.MerchantId == u.MerchantId && x.CreatorId == request.CreatorId).Select(x => x.Status).ToListAsync(ct);
        if (existing.Contains(PartnershipStatus.Blocked)) return Problem(409, "This relationship is blocked.");
        if (existing.Any(x => x is PartnershipStatus.Pending or PartnershipStatus.Approved)) return Problem(409, "A pending or active relationship with this creator already exists.");
        var now = DateTime.UtcNow; var p = new MerchantCreatorPartnership { Id = Guid.NewGuid(), MerchantId = u.MerchantId!.Value, CreatorId = request.CreatorId, RequestedAtUtc = now, RequestedByUserId = u.UserAccountId, IntroductoryMessage = request.IntroductoryMessage?.Trim(), CreatedAtUtc = now, CreatedBy = u.UserAccountId.ToString() };
        p.Approve(now, u.UserAccountId!.Value, request.StartDateUtc, request.EndDateUtc); db.Add(p); Audit(db, p, u.UserAccountId.Value, PartnershipStatus.Pending, PartnershipStatus.Approved, "MerchantDirectlyAddedCreator", null, h, now);
        if (request.LocationIds?.Length > 0) { var valid = await db.MerchantLocations.Where(x => x.MerchantId == u.MerchantId && x.IsActive && request.LocationIds.Distinct().Contains(x.Id)).Select(x => x.Id).ToListAsync(ct); if (valid.Count != request.LocationIds.Distinct().Count()) return Problem(400, "Every location must be active and belong to this merchant."); foreach (var id in valid) p.Locations.Add(NewLocation(p.Id, id, u.UserAccountId.Value, now)); }
        await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/merchant/partnerships/{p.Id}", Item(p, await db.Merchants.FindAsync([u.MerchantId.Value], ct) ?? new(), creator) with { RelationshipState = "AwaitingVideo", ActivationRequired = true });
    }

    private static async Task<IResult> Transition(Guid id, PartnershipStatus target, string? reason, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct, bool reactivationOnly = false)
    {
        var p = await db.MerchantCreatorPartnerships.Include(x => x.Merchant).Include(x => x.Creator).FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct); if (p is null) return NotFound();
        if (reactivationOnly && p.Status is not (PartnershipStatus.Suspended or PartnershipStatus.Revoked)) return Problem(409, p.Status == PartnershipStatus.Blocked ? "A blocked relationship cannot be reactivated." : "Only a deactivated relationship can be reactivated.");
        if (p.Status == PartnershipStatus.Pending && target is PartnershipStatus.Approved or PartnershipStatus.Rejected && await db.UserAccounts.AnyAsync(x => x.Id == p.RequestedByUserId && x.MerchantId == p.MerchantId, ct)) return Problem(409, "The invited creator must accept or decline this invitation.");
        if (target == PartnershipStatus.Approved && p.Creator.Status != CreatorStatus.Active) return Problem(400, "An inactive creator cannot be approved.");
        if (target == PartnershipStatus.Approved)
        {
            var eligibility = await BusinessAdvertisingEligibility.EvaluateAsync(db, p.MerchantId, campaignOptions.Value.CurrencyCode, ct);
            if (!eligibility.Eligible) return Problem(409, $"Advertising is restricted until the available wallet balance reaches {eligibility.Minimum:0.00} {campaignOptions.Value.CurrencyCode}.");
        }
        var old = p.Status; var now = DateTime.UtcNow;
        try { switch (target) { case PartnershipStatus.Approved when old == PartnershipStatus.Pending: p.Approve(now, u.UserAccountId!.Value, p.StartDateUtc, p.EndDateUtc); break; case PartnershipStatus.Approved when old == PartnershipStatus.Approved: break; case PartnershipStatus.Approved: p.Reactivate(now, u.UserAccountId!.Value); break; case PartnershipStatus.Rejected: p.Reject(now, u.UserAccountId!.Value, reason ?? "Rejected by merchant"); break; case PartnershipStatus.Suspended: p.Suspend(now, u.UserAccountId!.Value, reason ?? "Suspended by merchant"); await SuspendPromotion(p.Id, db, now, ct); break; case PartnershipStatus.Revoked: p.Revoke(now, u.UserAccountId!.Value); break; case PartnershipStatus.Blocked: p.Block(now, u.UserAccountId!.Value); break; default: throw new InvalidOperationException("Unsupported transition."); } }
        catch (CommissionConfigurationException ex) { db.ChangeTracker.Clear(); return Problem(409, $"Advertising cannot be activated until Platform commission settings are configured. {ex.Message}"); }
        catch (InvalidOperationException ex) { db.MerchantAuditEvents.Add(new MerchantAuditEvent { Id = Guid.NewGuid(), MerchantId = p.MerchantId, ActorUserAccountId = u.UserAccountId, EventType = "PartnershipInvalidTransitionAttempt", Detail = $"{old} -> {target}: {ex.Message}", CreatedAtUtc = now }); await db.SaveChangesAsync(ct); return Problem(409, ex.Message); }
        Audit(db, p, u.UserAccountId!.Value, old, target, $"Partnership{target}", reason, h, now);
        if (target is PartnershipStatus.Approved or PartnershipStatus.Rejected)
        {
            var reactivated = reactivationOnly && old is PartnershipStatus.Suspended or PartnershipStatus.Revoked;
            await NotifyCreator(notifications, db, p.CreatorId, target == PartnershipStatus.Approved ? NotificationType.PartnershipApproved : NotificationType.PartnershipRejected, reactivated ? $"partnership:{p.Id}:merchant:reactivated:{now.Ticks}" : $"partnership:{p.Id}:merchant:{target}", reactivated ? "Advertising relationship reactivated" : target == PartnershipStatus.Approved ? "Promotion request approved" : "Promotion request declined", reactivated ? $"{p.Merchant.TradingName} reactivated your advertising relationship." : $"{p.Merchant.TradingName} has {(target == PartnershipStatus.Approved ? "approved" : "declined")} your promotion request.", target == PartnershipStatus.Approved ? "/?view=ads" : "/?view=find", h.TraceIdentifier, p.Id, ct);
        }
        await db.SaveChangesAsync(ct); return Results.Ok(Item(p) with { RelationshipState = target == PartnershipStatus.Approved ? "AwaitingVideo" : State(p.Status), ActivationRequired = target == PartnershipStatus.Approved });
    }

    private static async Task<IResult> ReconcileReadiness(Guid id, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, CancellationToken ct)
    {
        var p = await db.MerchantCreatorPartnerships.Include(x => x.Merchant).Include(x => x.Creator).FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct);
        if (p is null) return NotFound();
        if (p.Status != PartnershipStatus.Approved) return Problem(409, "Only an approved partnership can be reconciled.");
        return Results.Ok(Item(p) with { RelationshipState = "AwaitingVideo", ActivationRequired = true });
    }

    private static async Task<IResult> CreatorPromotionVideos(ICurrentUserService u, ApplicationDbContext db, CancellationToken ct)
    {
        var creatorId = u.CreatorId!.Value;
        var partnerships = await db.MerchantCreatorPartnerships.AsNoTracking()
            .Include(x => x.Merchant)
            .Include(x => x.Creator).ThenInclude(x => x.SocialProfiles)
            .Include(x => x.Locations).ThenInclude(x => x.MerchantLocation)
            .Where(x => x.CreatorId == creatorId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .ToListAsync(ct);
        var ids = partnerships.Select(x => x.Id).ToArray();
        var videos = await db.PromotionVideos.AsNoTracking().Where(x => ids.Contains(x.MerchantCreatorPartnershipId)).OrderByDescending(x => x.SubmittedAtUtc).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var active = await ActivePartnershipIds(db, ids, now, ct);
        return Results.Ok(partnerships.Select(x => ItemWithPromotion(x, videos, active.Contains(x.Id) && x.IsTransactionEligibleAt(now), now)));
    }

    private static async Task<IResult> MerchantPromotionVideos(string? status, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct)
    {
        var merchantId = u.MerchantId!.Value;
        var query = db.MerchantCreatorPartnerships.AsNoTracking()
            .Include(x => x.Merchant)
            .Include(x => x.Creator).ThenInclude(x => x.SocialProfiles)
            .Include(x => x.Locations).ThenInclude(x => x.MerchantLocation)
            .Where(x => x.MerchantId == merchantId);
        if (Enum.TryParse<PartnershipStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed);
        var partnerships = await query.OrderByDescending(x => x.RequestedAtUtc).ToListAsync(ct);
        var ids = partnerships.Select(x => x.Id).ToArray();
        var videos = await db.PromotionVideos.AsNoTracking().Where(x => ids.Contains(x.MerchantCreatorPartnershipId)).OrderByDescending(x => x.SubmittedAtUtc).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var active = await ActivePartnershipIds(db, ids, now, ct);
        return Results.Ok(partnerships.Select(x => ItemWithPromotion(x, videos, active.Contains(x.Id) && x.IsTransactionEligibleAt(now), now)));
    }

    private static async Task<IResult> SubmitPromotionVideo(Guid partnershipId, SubmitPromotionVideoRequest request, ICurrentUserService u, ApplicationDbContext db, INotificationService notifications, ITikTokVideoUrlResolver videoUrls, IOptions<CampaignOptions> campaignOptions, HttpContext h, CancellationToken ct)
    {
        if (u.CreatorId is null) return Problem(401, "Creator authentication is required.");
        var now = DateTime.UtcNow;
        var partnership = await db.MerchantCreatorPartnerships.Include(x => x.Merchant).Include(x => x.Creator).FirstOrDefaultAsync(x => x.Id == partnershipId && x.CreatorId == u.CreatorId.Value, ct);
        if (partnership is null) return NotFound();
        if (partnership.Status != PartnershipStatus.Approved) return Problem(409, "Business permission is required before submitting a promotion video.");
        var resolvedVideo = await videoUrls.ResolveAsync(request.VideoUrl, ct);
        if (!resolvedVideo.Accepted) return Problem(400, resolvedVideo.Error);
        var currencyCode = campaignOptions.Value.CurrencyCode;
        var eligibility = await BusinessAdvertisingEligibility.EvaluateAsync(db, partnership.MerchantId, currencyCode, ct);
        if (!eligibility.Eligible) return Problem(409, $"Advertising is restricted until the available wallet balance reaches {eligibility.Minimum:0.00} {currencyCode}.");
        var existing = await db.PromotionVideos.FirstOrDefaultAsync(x => x.MerchantCreatorPartnershipId == partnership.Id && x.Status == PromotionVideoStatus.Pending, ct);
        if (existing is not null)
        {
            return Results.Ok(new { id = existing.Id, videoUrl = existing.VideoUrl, platform = existing.Platform, status = existing.Status.ToString(), submittedAtUtc = existing.SubmittedAtUtc, reviewedAtUtc = existing.ReviewedAtUtc, rejectionReason = existing.RejectionReason });
        }
        var video = new PromotionVideo
        {
            Id = Guid.NewGuid(),
            MerchantCreatorPartnershipId = partnership.Id,
            MerchantId = partnership.MerchantId,
            CreatorId = partnership.CreatorId,
            VideoUrl = resolvedVideo.VideoUrl!,
            Platform = "TikTok",
            SubmittedAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = u.UserAccountId?.ToString()
        };
        db.PromotionVideos.Add(video);
        await db.SaveChangesAsync(ct);
        await NotifyMerchant(notifications, db, partnership.MerchantId, NotificationType.PromotionVideoSubmitted, $"promotion-video:{video.Id}:submitted", "New promotion video waiting for approval.", $"{partnership.Creator.DisplayName} submitted a TikTok promotion video for approval.", "/?view=requests", h.TraceIdentifier, partnership.Id, ct);
        return Results.Created($"/api/v1/creator/partnerships/{partnership.Id}/promotion-video", new { id = video.Id, videoUrl = video.VideoUrl, platform = video.Platform, status = video.Status.ToString(), submittedAtUtc = video.SubmittedAtUtc, reviewedAtUtc = video.ReviewedAtUtc, rejectionReason = video.RejectionReason });
    }

    private static async Task<IResult> ReviewPromotionVideo(Guid videoId, bool approve, PromotionVideoActionRequest request, ICurrentUserService u, ApplicationDbContext db, INotificationService notifications, HttpContext h, CancellationToken ct)
    {
        if (u.MerchantId is null) return Problem(401, "Merchant authentication is required.");
        var video = await db.PromotionVideos.Include(x => x.MerchantCreatorPartnership).ThenInclude(x => x.Creator).Include(x => x.MerchantCreatorPartnership).ThenInclude(x => x.Merchant).FirstOrDefaultAsync(x => x.Id == videoId && x.MerchantId == u.MerchantId.Value, ct);
        if (video is null) return NotFound();
        if (video.Status != PromotionVideoStatus.Pending) return Problem(409, "Only a pending promotion video can be reviewed.");
        var now = DateTime.UtcNow;
        if (approve)
        {
            video.Approve(now, u.UserAccountId!.Value);
            video.MerchantCreatorPartnership.PreparePromotion(now, u.UserAccountId.Value);
            await SuspendPromotion(video.MerchantCreatorPartnershipId, db, now, ct);
            await db.SaveChangesAsync(ct);
            await NotifyCreator(notifications, db, video.CreatorId, NotificationType.PromotionVideoApproved, $"promotion-video:{video.Id}:approved", "Promotion video approved", $"Your promotion video for {video.MerchantCreatorPartnership.Merchant.TradingName} is approved and ready to go live.", "/?view=ads", h.TraceIdentifier, video.MerchantCreatorPartnershipId, ct);
            return Results.Ok(new { id = video.Id, videoUrl = video.VideoUrl, platform = video.Platform, status = video.Status.ToString(), submittedAtUtc = video.SubmittedAtUtc, reviewedAtUtc = video.ReviewedAtUtc, rejectionReason = video.RejectionReason });
        }
        var rejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? "Please contact the business for more information." : request.Reason.Trim();
        video.Reject(now, u.UserAccountId!.Value, rejectionReason);
        await db.SaveChangesAsync(ct);
        await NotifyCreator(notifications, db, video.CreatorId, NotificationType.PromotionVideoRejected, $"promotion-video:{video.Id}:rejected", "Promotion video rejected", $"Your promotion video for {video.MerchantCreatorPartnership.Merchant.TradingName} was not approved. {rejectionReason}", "/?view=ads", h.TraceIdentifier, video.MerchantCreatorPartnershipId, ct);
        return Results.Ok(new { id = video.Id, videoUrl = video.VideoUrl, platform = video.Platform, status = video.Status.ToString(), submittedAtUtc = video.SubmittedAtUtc, reviewedAtUtc = video.ReviewedAtUtc, rejectionReason = video.RejectionReason });
    }

    private static PromotionVideoSummaryDto? CurrentPromotionVideo(IEnumerable<PromotionVideo> videos, Guid partnershipId, bool promotionActive)
    {
        var current = videos.Where(x => x.MerchantCreatorPartnershipId == partnershipId)
            .OrderByDescending(x => x.Status == PromotionVideoStatus.Pending)
            .ThenByDescending(x => x.Status == PromotionVideoStatus.Approved)
            .ThenByDescending(x => x.SubmittedAtUtc)
            .FirstOrDefault();
        if (current is null) return null;
        var status = current.Status switch
        {
            PromotionVideoStatus.Pending => "Pending",
            PromotionVideoStatus.Rejected => "Rejected",
            PromotionVideoStatus.Expired => "Expired",
            PromotionVideoStatus.Approved when promotionActive => "Live",
            PromotionVideoStatus.Approved => "Approved",
            _ => "Pending"
        };
        return new(current.Id, current.VideoUrl, current.Platform, status, current.SubmittedAtUtc, current.ReviewedAtUtc, current.RejectionReason);
    }

    private static async Task<IResult> GoLive(Guid partnershipId, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, CancellationToken ct)
    {
        if (u.CreatorId is null || u.UserAccountId is null) return Problem(401, "Creator authentication is required.");
        var partnership = await db.MerchantCreatorPartnerships.Include(x => x.Merchant).Include(x => x.Creator).FirstOrDefaultAsync(x => x.Id == partnershipId && x.CreatorId == u.CreatorId.Value, ct);
        if (partnership is null) return NotFound();
        if (partnership.Status != PartnershipStatus.Approved) return Problem(409, "Business permission is required before going live.");

        var now = DateTime.UtcNow;
        var creatorEligible = partnership.Creator.Status == CreatorStatus.Active && await db.UserAccounts.AsNoTracking().AnyAsync(x => x.CreatorId == partnership.CreatorId && x.Role == UserRole.Creator && x.Status == AccountStatus.Active && (x.LockoutEndUtc == null || x.LockoutEndUtc <= now), ct);
        if (!creatorEligible) return Problem(409, "An active, eligible Creator account is required before going live.");
        if (!await IsActivationReady(partnership, db, campaignOptions.Value, ct)) return Problem(409, "The Business is not currently eligible to publish this promotion.");

        var video = await db.PromotionVideos.Where(x => x.MerchantCreatorPartnershipId == partnership.Id).OrderByDescending(x => x.SubmittedAtUtc).FirstOrDefaultAsync(ct);
        if (video?.Status != PromotionVideoStatus.Approved) return Problem(409, "An approved promotion video is required before going live.");

        var alreadyLive = await db.CreatorMerchantCampaigns.AnyAsync(x => x.MerchantCreatorPartnershipId == partnership.Id && x.Status == CampaignStatus.Active && x.StartsAtUtc <= now && x.ExpiresAtUtc > now, ct);
        if (!alreadyLive)
        {
            partnership.ActivatePromotion(now, u.UserAccountId.Value);
            try { await EnsurePromotion(partnership, u.UserAccountId.Value, db, commissions, campaignOptions.Value, now, ct); }
            catch (CommissionConfigurationException ex) { db.ChangeTracker.Clear(); return Problem(409, $"Advertising cannot go live until Platform commission settings are configured. {ex.Message}"); }
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(Item(partnership) with
        {
            StartDateUtc = partnership.StartDateUtc,
            EndDateUtc = partnership.EndDateUtc,
            ActivatedAtUtc = partnership.StartDateUtc,
            ExpiresAtUtc = partnership.EndDateUtc,
            PromotionActive = true,
            RelationshipState = "Active",
            ActivationRequired = false,
            PromotionVideo = CurrentPromotionVideo([video], partnership.Id, true)
        });
    }

    private static async Task<IResult> CreatorRevoke(Guid id, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct, string eventType)
    { var p = await db.MerchantCreatorPartnerships.Include(x => x.Merchant).Include(x => x.Creator).FirstOrDefaultAsync(x => x.Id == id && x.CreatorId == u.CreatorId, ct); if (p is null) return NotFound(); if (eventType == "PartnershipWithdrawn" && p.Status != PartnershipStatus.Pending) return Problem(409, "Only a pending request can be withdrawn."); if (eventType == "CreatorStoppedPromoting" && p.Status != PartnershipStatus.Approved) return Problem(409, "Only an approved partnership can be stopped."); var old = p.Status; var now = DateTime.UtcNow; p.Revoke(now, u.UserAccountId!.Value); Audit(db, p, u.UserAccountId.Value, old, PartnershipStatus.Revoked, eventType, null, h, now); await db.SaveChangesAsync(ct); return Results.Ok(Item(p)); }

    private static async Task<IResult> SetDates(Guid id, PartnershipDatesRequest r, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct)
    { var p = await db.MerchantCreatorPartnerships.FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct); if (p is null) return NotFound(); try { p.SetDates(r.StartDateUtc, r.EndDateUtc, DateTime.UtcNow, u.UserAccountId!.Value); } catch (ArgumentException ex) { return Problem(400, ex.Message); } AddMerchantAudit(db, p, "PartnershipDatesChanged", u.UserAccountId, r.ToString()); await db.SaveChangesAsync(ct); return Results.Ok(Item(p)); }
    private static async Task<IResult> SetLocations(Guid id, PartnershipLocationsRequest r, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct)
    { var p = await db.MerchantCreatorPartnerships.Include(x => x.Locations).FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct); if (p is null) return NotFound(); var ids = r.LocationIds.Distinct().ToArray(); var valid = await db.MerchantLocations.Where(x => x.MerchantId == u.MerchantId && x.IsActive && ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct); if (valid.Count != ids.Length) { AddMerchantAudit(db, p, "PartnershipCrossMerchantLocationAttempt", u.UserAccountId, null); await db.SaveChangesAsync(ct); return Problem(400, "Every location must be active and belong to this merchant."); } var now = DateTime.UtcNow; foreach (var old in p.Locations) old.IsActive = ids.Contains(old.MerchantLocationId); foreach (var add in ids.Where(x => p.Locations.All(y => y.MerchantLocationId != x))) p.Locations.Add(NewLocation(p.Id, add, u.UserAccountId!.Value, now)); AddMerchantAudit(db, p, "PartnershipLocationsChanged", u.UserAccountId, string.Join(',', ids)); await db.SaveChangesAsync(ct); return Results.Ok(Item(p)); }

    private static async Task<bool> IsActivationReady(MerchantCreatorPartnership partnership, ApplicationDbContext db, CampaignOptions options, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var merchantReady = await db.Merchants.AsNoTracking().AnyAsync(x => x.Id == partnership.MerchantId && x.Status == MerchantStatus.Active, ct);
        var merchantAccountReady = await db.UserAccounts.AsNoTracking().AnyAsync(x => x.MerchantId == partnership.MerchantId && x.Role == UserRole.MerchantAdmin && x.Status == AccountStatus.Active && (x.LockoutEndUtc == null || x.LockoutEndUtc <= now), ct);
        var creatorReady = await db.Creators.AsNoTracking().AnyAsync(x => x.Id == partnership.CreatorId && x.Status == CreatorStatus.Active, ct)
            && await db.UserAccounts.AsNoTracking().AnyAsync(x => x.CreatorId == partnership.CreatorId && x.Role == UserRole.Creator && x.Status == AccountStatus.Active && (x.LockoutEndUtc == null || x.LockoutEndUtc <= now), ct);
        if (!merchantReady || !merchantAccountReady || !creatorReady) return false;
        return (await BusinessAdvertisingEligibility.EvaluateAsync(db, partnership.MerchantId, options.CurrencyCode, ct)).Eligible;
    }

    private static async Task EnsurePromotion(MerchantCreatorPartnership partnership, Guid actor, ApplicationDbContext db, ICommissionEngine commissions, CampaignOptions options, DateTime now, CancellationToken ct)
    {
        var alreadyActive = await db.CreatorMerchantCampaigns.AnyAsync(x => x.MerchantCreatorPartnershipId == partnership.Id && x.Status == CampaignStatus.Active && x.StartsAtUtc <= now && x.ExpiresAtUtc > now, ct);
        if (alreadyActive) return;
        var reusable = await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == partnership.Id && x.Status == CampaignStatus.Suspended).OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (reusable is not null) { reusable.Reactivate(now); partnership.AssignedCampaignId = reusable.Id; return; }
        var merchant = partnership.Merchant is { Id: var loadedMerchantId } && loadedMerchantId == partnership.MerchantId ? partnership.Merchant : await db.Merchants.SingleAsync(x => x.Id == partnership.MerchantId, ct);
        var selected = await commissions.SelectAsync(new(0m, partnership.MerchantId, partnership.CreatorId, partnership.Id, null, options.CurrencyCode, now), ct);
        var campaign = new CreatorMerchantCampaign
        {
            Id = Guid.NewGuid(),
            PublicCampaignId = $"CMP-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            CreatorId = partnership.CreatorId,
            MerchantId = partnership.MerchantId,
            MerchantCreatorPartnershipId = partnership.Id,
            CreatedAtUtc = now,
            CreatedBy = actor.ToString()
        };
        campaign.Approve(options.DefaultDurationDays, null, selected.Version.Id, actor, Convert.ToHexString(RandomNumberGenerator.GetBytes(5)), $"{merchant.TradingName} Creator promotion", now, BusinessTypes.SuggestedReuseRule(merchant.BusinessType) ?? OfferReuseRule.OncePerOffer);
        campaign.Start(now);
        partnership.AssignedCampaignId = campaign.Id;
        db.CreatorMerchantCampaigns.Add(campaign);
        db.CampaignCommissionAssignments.Add(new CampaignCommissionAssignment { Id = Guid.NewGuid(), CampaignId = campaign.Id, MerchantCreatorPartnershipId = partnership.Id, CommissionRuleId = selected.Rule.Id, EffectiveFromUtc = now, IsActive = true, CreatedAtUtc = now, CreatedBy = actor.ToString() });
        db.OperationalAuditEvents.Add(new OperationalAuditEvent { Id = Guid.NewGuid(), EventType = "PartnershipPromotionActivated", ActorUserId = actor, MerchantId = partnership.MerchantId, SubjectId = campaign.Id, CorrelationId = Guid.NewGuid().ToString("N"), CreatedAtUtc = now, CreatedBy = actor.ToString() });
    }

    private static async Task SuspendPromotion(Guid partnershipId, ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        foreach (var campaign in await db.CreatorMerchantCampaigns.Where(x => x.MerchantCreatorPartnershipId == partnershipId && (x.Status == CampaignStatus.Active || x.Status == CampaignStatus.Scheduled || x.Status == CampaignStatus.ApprovedAwaitingStart)).ToListAsync(ct)) campaign.Suspend(now);
    }

    private static IQueryable<MerchantCreatorPartnership> Query(ApplicationDbContext db) => db.MerchantCreatorPartnerships.AsNoTracking().Include(x => x.Merchant).Include(x => x.Creator).ThenInclude(x => x.SocialProfiles).Include(x => x.Locations).ThenInclude(x => x.MerchantLocation).OrderByDescending(x => x.RequestedAtUtc);
    private static async Task<IReadOnlyList<PartnershipListItem>> ItemsWithInitiator(IQueryable<MerchantCreatorPartnership> query, ApplicationDbContext db, string currencyCode, CancellationToken ct)
    {
        var items = await query.ToListAsync(ct);
        var requesterIds = items.Select(x => x.RequestedByUserId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var businessRequesters = await db.UserAccounts.AsNoTracking().Where(x => requesterIds.Contains(x.Id) && x.MerchantId != null).Select(x => x.Id).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var eligibleMerchantIds = await BusinessAdvertisingEligibility.EligibleMerchantIdsAsync(db, items.Select(x => x.MerchantId), currencyCode, ct);
        var ids = items.Select(x => x.Id).ToArray();
        var active = await ActivePartnershipIds(db, ids, now, ct);
        var videos = await db.PromotionVideos.AsNoTracking().Where(x => ids.Contains(x.MerchantCreatorPartnershipId)).OrderByDescending(x => x.SubmittedAtUtc).ToListAsync(ct);
        return items.Select(x =>
        {
            var current = CurrentPromotionVideo(videos, x.Id, false);
            var promotionActive = active.Contains(x.Id) && x.IsTransactionEligibleAt(now) && eligibleMerchantIds.Contains(x.MerchantId) && current?.Status == "Approved";
            var item = ItemWithPromotion(x, videos, promotionActive, now);
            return item with { InitiatedBy = x.RequestedByUserId.HasValue && businessRequesters.Contains(x.RequestedByUserId.Value) ? "Business" : "Creator" };
        }).ToList();
    }

    private static async Task<HashSet<Guid>> ActivePartnershipIds(ApplicationDbContext db, Guid[] ids, DateTime now, CancellationToken ct) =>
        (await db.CreatorMerchantCampaigns.AsNoTracking().Where(x => ids.Contains(x.MerchantCreatorPartnershipId) && x.Status == CampaignStatus.Active && x.StartsAtUtc <= now && x.ExpiresAtUtc > now).Select(x => x.MerchantCreatorPartnershipId).Distinct().ToListAsync(ct)).ToHashSet();

    private static PartnershipListItem ItemWithPromotion(MerchantCreatorPartnership partnership, IEnumerable<PromotionVideo> videos, bool promotionActive, DateTime now)
    {
        var video = CurrentPromotionVideo(videos, partnership.Id, promotionActive);
        var relationshipState = partnership.Status == PartnershipStatus.Approved
            ? promotionActive ? "Active" : video?.Status switch { "Pending" => "PendingApproval", "Approved" => "Approved", "Rejected" => "Rejected", _ => "AwaitingVideo" }
            : State(partnership.Status);
        var item = Item(partnership);
        return item with
        {
            StartDateUtc = promotionActive ? partnership.StartDateUtc : null,
            EndDateUtc = promotionActive ? partnership.EndDateUtc : null,
            ActivatedAtUtc = promotionActive ? partnership.StartDateUtc : null,
            ExpiresAtUtc = promotionActive ? partnership.EndDateUtc : null,
            PromotionActive = promotionActive,
            RelationshipState = relationshipState,
            ActivationRequired = partnership.Status == PartnershipStatus.Approved && !promotionActive,
            PromotionVideo = video
        };
    }
    private static string State(PartnershipStatus status) => status switch { PartnershipStatus.Pending => "Pending", PartnershipStatus.Rejected => "Declined", PartnershipStatus.Approved => "ActivationRequired", PartnershipStatus.Suspended => "Suspended", PartnershipStatus.Revoked => "Revoked", PartnershipStatus.Blocked => "Blocked", _ => "NoRelationship" };
    private static PartnershipListItem Item(MerchantCreatorPartnership p) => Item(p, p.Merchant, p.Creator);
    private static PartnershipListItem Item(MerchantCreatorPartnership p, Merchant m, Creator c)
    {
        var social = c.SocialProfiles.OrderByDescending(s => s.IsPrimary).ThenByDescending(s => s.VerificationStatus).ThenByDescending(s => s.FollowerCount).FirstOrDefault();
        return new(p.Id, p.MerchantId, m.TradingName, p.CreatorId, c.DisplayName, p.Status, p.RequestedAtUtc, p.StartDateUtc, p.EndDateUtc, p.IntroductoryMessage, p.Locations.Where(x => x.IsActive).Select(x => new LocationItem(x.MerchantLocationId, x.MerchantLocation?.Name ?? "Location", x.MerchantLocation?.IsActive ?? true)).ToList(), ActivatedAtUtc: p.StartDateUtc, ExpiresAtUtc: p.EndDateUtc, CreatorSocialPlatform: social?.Platform.ToString(), CreatorSocialProfileUrl: social?.ProfileUrl, CreatorCity: c.City, CreatorFollowerCount: social?.FollowerCount, CreatorProfileImageUrl: c.ProfileImageFileName is null ? null : $"/api/v1/discovery/creators/{Uri.EscapeDataString(c.PublicCreatorId)}/photo?v={Uri.EscapeDataString(c.ProfileImageFileName)}", CreatorPhoneNumber: c.PhoneNumber, CreatorPublicId: string.IsNullOrWhiteSpace(c.CreatorCode) ? c.PublicCreatorId : c.CreatorCode);
    }
    private static PartnershipLocation NewLocation(Guid p, Guid l, Guid user, DateTime now) => new() { Id = Guid.NewGuid(), MerchantCreatorPartnershipId = p, MerchantLocationId = l, IsActive = true, CreatedAtUtc = now, CreatedBy = user.ToString() };
    private static void Audit(ApplicationDbContext db, MerchantCreatorPartnership p, Guid user, PartnershipStatus old, PartnershipStatus next, string type, string? reason, HttpContext h, DateTime now) { db.PartnershipStatusHistories.Add(new() { Id = Guid.NewGuid(), MerchantCreatorPartnershipId = p.Id, PreviousStatus = old, NewStatus = next, ChangedAtUtc = now, ChangedByUserId = user, Reason = reason, CorrelationId = h.TraceIdentifier, CreatedAtUtc = now, CreatedBy = user.ToString() }); AddMerchantAudit(db, p, type, user, reason); }
    private static void AddMerchantAudit(ApplicationDbContext db, MerchantCreatorPartnership p, string type, Guid? user, string? detail) => db.MerchantAuditEvents.Add(new() { Id = Guid.NewGuid(), MerchantId = p.MerchantId, ActorUserAccountId = user, EventType = type, Detail = detail, CreatedAtUtc = DateTime.UtcNow, CreatedBy = user?.ToString() });
    private static async Task NotifyCreator(INotificationService notifications, ApplicationDbContext db, Guid creatorId, NotificationType type, string key, string title, string body, string target, string correlation, Guid entityId, CancellationToken ct)
    { var userId = await db.UserAccounts.Where(x => x.CreatorId == creatorId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct); if (userId.HasValue) await notifications.CreateAsync(new(type, key, new Dictionary<string, string> { { "Title", title }, { "Body", body }, { "TargetPath", target } }, [new(userId, NotificationRecipientType.User, NotificationChannel.InApp, null, null)], NotificationPriority.Normal, correlation, "Partnership", entityId.ToString()), ct); }
    private static async Task NotifyMerchant(INotificationService notifications, ApplicationDbContext db, Guid merchantId, NotificationType type, string key, string title, string body, string target, string correlation, Guid entityId, CancellationToken ct)
    { var users = await db.UserAccounts.Where(x => x.MerchantId == merchantId && x.Role == UserRole.MerchantAdmin && x.Status == AccountStatus.Active).Select(x => x.Id).ToListAsync(ct); if (users.Count > 0) await notifications.CreateAsync(new(type, key, new Dictionary<string, string> { { "Title", title }, { "Body", body }, { "TargetPath", target } }, users.Select(x => new NotificationRecipientRequest(x, NotificationRecipientType.User, NotificationChannel.InApp, null, null)).ToList(), NotificationPriority.Normal, correlation, "Partnership", entityId.ToString()), ct); }
    private static IResult Problem(int status, string detail) => Results.Problem(statusCode: status, title: "Partnership request failed", detail: detail);
    private static IResult NotFound() => Problem(404, "Partnership was not found in your scope.");
}
