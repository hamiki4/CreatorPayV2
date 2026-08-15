using System.Security.Cryptography;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Campaigns;
using CreatorPay.Application.Commission;
using CreatorPay.Application.Merchants;
using CreatorPay.Application.Notifications;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPay.Api.Partnerships;

public sealed record CreatePartnershipRequest(Guid MerchantId, string? IntroductoryMessage, DateTime? RequestedStartDateUtc);
public sealed record MerchantAddCreatorRequest(Guid CreatorId, bool ConfirmApproval, string? IntroductoryMessage, DateTime? StartDateUtc, DateTime? EndDateUtc, Guid[]? LocationIds);
public sealed record MerchantInviteCreatorRequest(Guid CreatorId, string? IntroductoryMessage);
public sealed record PartnershipReasonRequest(string? Reason);
public sealed record PartnershipDatesRequest(DateTime? StartDateUtc, DateTime? EndDateUtc);
public sealed record PartnershipLocationsRequest(Guid[] LocationIds);
public sealed record PartnershipListItem(Guid Id, Guid MerchantId, string MerchantName, Guid CreatorId, string CreatorName, PartnershipStatus Status, DateTime RequestedAtUtc, DateTime? StartDateUtc, DateTime? EndDateUtc, string? IntroductoryMessage, IReadOnlyList<LocationItem> Locations, string InitiatedBy = "Unknown", DateTime? ActivatedAtUtc = null, DateTime? ExpiresAtUtc = null, bool PromotionActive = false);
public sealed record LocationItem(Guid Id, string Name, bool IsActive);

public static class PartnershipEndpoints
{
    public static IEndpointRouteBuilder MapPartnershipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var creator = endpoints.MapGroup("/api/v1/creator").WithTags("Creator partnerships").RequireAuthorization("CreatorOnly");
        creator.MapGet("/merchants/search", SearchMerchants);
        creator.MapPost("/partnerships/requests", RequestPartnership);
        creator.MapGet("/partnerships", CreatorPartnerships);
        creator.MapGet("/partnerships/{id:guid}", CreatorPartnership);
        creator.MapPost("/partnerships/{id:guid}/withdraw", (Guid id, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct) => CreatorRevoke(id, u, db, h, ct, "PartnershipWithdrawn"));
        creator.MapPost("/partnerships/{id:guid}/stop-promoting", (Guid id, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct) => CreatorRevoke(id, u, db, h, ct, "CreatorStoppedPromoting"));
        creator.MapPost("/partnerships/{id:guid}/accept-invitation", (Guid id, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => CreatorInvitationDecision(id, true, u, db, commissions, campaignOptions, notifications, h, ct));
        creator.MapPost("/partnerships/{id:guid}/decline-invitation", (Guid id, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => CreatorInvitationDecision(id, false, u, db, commissions, campaignOptions, notifications, h, ct));

        var merchant = endpoints.MapGroup("/api/v1/merchant").WithTags("Merchant partnerships").RequireAuthorization("MerchantAdminOnly");
        merchant.MapGet("/creators/search", SearchCreators);
        merchant.MapGet("/partnerships", MerchantPartnerships);
        merchant.MapGet("/dashboard-metrics", MerchantDashboardMetrics);
        merchant.MapGet("/partnerships/{id:guid}", MerchantPartnership);
        merchant.MapPost("/partnerships", MerchantAdd);
        merchant.MapPost("/partnerships/invitations", MerchantInvite);
        merchant.MapPost("/partnerships/{id:guid}/approve", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Approved, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/reject", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Rejected, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/suspend", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Suspended, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/activate", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Approved, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/reactivate", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Approved, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/revoke", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Revoked, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPost("/partnerships/{id:guid}/block", (Guid id, PartnershipReasonRequest r, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct) => Transition(id, PartnershipStatus.Blocked, r.Reason, u, db, commissions, campaignOptions, notifications, h, ct));
        merchant.MapPut("/partnerships/{id:guid}/locations", SetLocations);
        merchant.MapPut("/partnerships/{id:guid}/dates", SetDates);

        var admin = endpoints.MapGroup("/api/v1/admin/partnerships").WithTags("Partnership support").RequireAuthorization("PlatformAdminOnly");
        admin.MapGet("/", async (ApplicationDbContext db, CancellationToken ct) => Results.Ok((await Query(db).ToListAsync(ct)).Select(Item)));
        admin.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db, CancellationToken ct) => await Query(db).FirstOrDefaultAsync(x => x.Id == id, ct) is { } p ? Results.Ok(Item(p)) : NotFound());
        return endpoints;
    }

    private static async Task<IResult> SearchMerchants(string? q, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct)
    {
        var creatorId = user.CreatorId!.Value; q = q?.Trim();
        var blocked = db.MerchantCreatorPartnerships.Where(x => x.CreatorId == creatorId && x.Status == PartnershipStatus.Blocked).Select(x => x.MerchantId);
        var query = db.Merchants.AsNoTracking().Where(x => x.Status == MerchantStatus.Active && db.UserAccounts.Any(a => a.MerchantId == x.Id && a.Role == UserRole.MerchantAdmin && a.Status == AccountStatus.Active) && !blocked.Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => EF.Functions.ILike(x.TradingName, $"%{q}%") || EF.Functions.ILike(x.LegalBusinessName, $"%{q}%") || EF.Functions.ILike(x.PublicMerchantId, $"%{q}%") || EF.Functions.ILike(x.City, $"%{q}%") || EF.Functions.ILike(x.BusinessType, $"%{q}%"));
        return Results.Ok(await query.OrderBy(x => x.TradingName).Take(50).Select(x => new { x.Id, x.PublicMerchantId, x.TradingName, x.LegalBusinessName, x.City, x.BusinessType }).ToListAsync(ct));
    }

    private static async Task<IResult> RequestPartnership(CreatePartnershipRequest request, ICurrentUserService user, ApplicationDbContext db, INotificationService notifications, HttpContext http, CancellationToken ct)
    {
        var now = DateTime.UtcNow; var creatorId = user.CreatorId!.Value;
        var creator = await db.Creators.FindAsync([creatorId], ct); var merchant = await db.Merchants.FindAsync([request.MerchantId], ct);
        if (creator?.Status != CreatorStatus.Active) return Problem(403, "Creator is not active.");
        if (merchant?.Status != MerchantStatus.Active) return Problem(400, "Merchant is not eligible to receive requests.");
        var existing = await db.MerchantCreatorPartnerships.SingleOrDefaultAsync(x => x.CreatorId == creatorId && x.MerchantId == request.MerchantId, ct);
        if (existing is not null) return Problem(409, existing.Status == PartnershipStatus.Blocked ? "This relationship is blocked." : "A relationship with this merchant already exists.");
        var p = new MerchantCreatorPartnership { Id = Guid.NewGuid(), CreatorId = creatorId, MerchantId = request.MerchantId, RequestedAtUtc = now, RequestedByUserId = user.UserAccountId, IntroductoryMessage = request.IntroductoryMessage?.Trim(), CreatedAtUtc = now, CreatedBy = user.UserAccountId.ToString() };
        if (request.RequestedStartDateUtc.HasValue) p.SetDates(request.RequestedStartDateUtc, null, now, user.UserAccountId!.Value);
        db.Add(p); Audit(db, p, user.UserAccountId!.Value, PartnershipStatus.Pending, PartnershipStatus.Pending, "PartnershipRequested", null, http, now);
        await NotifyMerchant(notifications, db, p.MerchantId, NotificationType.PartnershipRequested, $"partnership:{p.Id}:requested", "New Creator request", $"{creator.DisplayName} requested permission to promote your Business.", "/?view=requests", http.TraceIdentifier, p.Id, ct); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/creator/partnerships/{p.Id}", Item(p, merchant, creator));
    }

    private static async Task<IResult> CreatorPartnerships(string? status, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct)
    { var query = Query(db).Where(x => x.CreatorId == u.CreatorId); if (Enum.TryParse<PartnershipStatus>(status, true, out var s)) query = query.Where(x => x.Status == s); return Results.Ok(await ItemsWithInitiator(query, db, ct)); }
    private static async Task<IResult> CreatorPartnership(Guid id, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => await Query(db).FirstOrDefaultAsync(x => x.Id == id && x.CreatorId == u.CreatorId, ct) is { } p ? Results.Ok(Item(p)) : NotFound();

    private static async Task<IResult> SearchCreators(string? q, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct)
    {
        q = q?.Trim();
        var query = db.Creators.AsNoTracking().Where(x => x.Status == CreatorStatus.Active && db.UserAccounts.Any(u => u.CreatorId == x.Id && u.Status == AccountStatus.Active));
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => EF.Functions.ILike(x.DisplayName, $"%{q}%") || EF.Functions.ILike(x.PublicCreatorId, $"%{q}%") || x.SocialProfiles.Any(s => EF.Functions.ILike(s.Handle, $"%{q}%")));
        return Results.Ok(await query.OrderBy(x => x.DisplayName).Take(50).Select(x => new
        {
            x.Id,
            x.PublicCreatorId,
            x.DisplayName,
            x.City,
            x.Biography,
            x.ContentCategories,
            SocialPlatform = x.SocialProfiles.OrderByDescending(s => s.IsPrimary).Select(s => (SocialPlatform?)s.Platform).FirstOrDefault(),
            FollowerCount = x.SocialProfiles.OrderByDescending(s => s.IsPrimary).Select(s => (long?)s.FollowerCount).FirstOrDefault()
        }).ToListAsync(ct));
    }
    private static async Task<IResult> MerchantPartnerships(string? status, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct)
    { var query = Query(db).Where(x => x.MerchantId == u.MerchantId); if (Enum.TryParse<PartnershipStatus>(status, true, out var s)) query = query.Where(x => x.Status == s); return Results.Ok(await ItemsWithInitiator(query, db, ct)); }
    private static async Task<IResult> MerchantDashboardMetrics(ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) { var now = DateTime.UtcNow; var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc); var sales = await db.PurchaseTransactions.CountAsync(x => x.MerchantId == u.MerchantId && x.TransactionDateUtc >= start && (x.Status == TransactionStatus.Confirmed || x.Status == TransactionStatus.Settled), ct); return Results.Ok(new { confirmedSales = sales, period = "This Month", periodStartedAtUtc = start }); }
    private static async Task<IResult> MerchantPartnership(Guid id, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => await Query(db).FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct) is { } p ? Results.Ok(Item(p)) : NotFound();

    private static async Task<IResult> MerchantInvite(MerchantInviteCreatorRequest request, ICurrentUserService u, ApplicationDbContext db, INotificationService notifications, HttpContext h, CancellationToken ct)
    {
        var creator = await db.Creators.FindAsync([request.CreatorId], ct); if (creator?.Status != CreatorStatus.Active) return Problem(400, "Only an active platform-approved creator can be invited.");
        if (await db.MerchantCreatorPartnerships.AnyAsync(x => x.MerchantId == u.MerchantId && x.CreatorId == request.CreatorId, ct)) return Problem(409, "An advertising relationship with this creator already exists.");
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
        var now = DateTime.UtcNow; var old = p.Status; if (accept) p.Approve(now, u.UserAccountId!.Value, p.StartDateUtc, p.EndDateUtc); else p.Reject(now, u.UserAccountId!.Value, "Declined by creator");
        Audit(db, p, u.UserAccountId.Value, old, p.Status, accept ? "CreatorAdvertisingInvitationAccepted" : "CreatorAdvertisingInvitationDeclined", null, h, now);
        await NotifyMerchant(notifications, db, p.MerchantId, accept ? NotificationType.PartnershipApproved : NotificationType.PartnershipRejected, $"partnership:{p.Id}:creator:{p.Status}", accept ? "Creator accepted your invitation" : "Creator declined your invitation", $"{p.Creator.DisplayName} has {(accept ? "accepted" : "declined")} your invitation.", "/?view=requests", h.TraceIdentifier, p.Id, ct);
        await db.SaveChangesAsync(ct); return Results.Ok(Item(p) with { InitiatedBy = "Business" });
    }

    private static async Task<IResult> MerchantAdd(MerchantAddCreatorRequest request, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, HttpContext h, CancellationToken ct)
    {
        if (!request.ConfirmApproval) return Problem(400, "Explicit approval confirmation is required.");
        var creator = await db.Creators.FindAsync([request.CreatorId], ct); if (creator?.Status != CreatorStatus.Active) return Problem(400, "Only an active platform-approved creator can be added.");
        if (await db.MerchantCreatorPartnerships.AnyAsync(x => x.MerchantId == u.MerchantId && x.CreatorId == request.CreatorId, ct)) return Problem(409, "A relationship with this creator already exists.");
        var now = DateTime.UtcNow; var p = new MerchantCreatorPartnership { Id = Guid.NewGuid(), MerchantId = u.MerchantId!.Value, CreatorId = request.CreatorId, RequestedAtUtc = now, RequestedByUserId = u.UserAccountId, IntroductoryMessage = request.IntroductoryMessage?.Trim(), CreatedAtUtc = now, CreatedBy = u.UserAccountId.ToString() };
        p.Approve(now, u.UserAccountId!.Value, request.StartDateUtc, request.EndDateUtc); db.Add(p); Audit(db, p, u.UserAccountId.Value, PartnershipStatus.Pending, PartnershipStatus.Approved, "MerchantDirectlyAddedCreator", null, h, now);
        if (request.LocationIds?.Length > 0) { var valid = await db.MerchantLocations.Where(x => x.MerchantId == u.MerchantId && x.IsActive && request.LocationIds.Distinct().Contains(x.Id)).Select(x => x.Id).ToListAsync(ct); if (valid.Count != request.LocationIds.Distinct().Count()) return Problem(400, "Every location must be active and belong to this merchant."); foreach (var id in valid) p.Locations.Add(NewLocation(p.Id, id, u.UserAccountId.Value, now)); }
        await db.SaveChangesAsync(ct); return Results.Created($"/api/v1/merchant/partnerships/{p.Id}", Item(p, await db.Merchants.FindAsync([u.MerchantId.Value], ct) ?? new(), creator) with { PromotionActive = false });
    }

    private static async Task<IResult> Transition(Guid id, PartnershipStatus target, string? reason, ICurrentUserService u, ApplicationDbContext db, ICommissionEngine commissions, IOptions<CampaignOptions> campaignOptions, INotificationService notifications, HttpContext h, CancellationToken ct)
    {
        var p = await db.MerchantCreatorPartnerships.Include(x => x.Merchant).Include(x => x.Creator).FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct); if (p is null) return NotFound();
        if (p.Status == PartnershipStatus.Pending && target is PartnershipStatus.Approved or PartnershipStatus.Rejected && await db.UserAccounts.AnyAsync(x => x.Id == p.RequestedByUserId && x.MerchantId == p.MerchantId, ct)) return Problem(409, "The invited creator must accept or decline this invitation.");
        if (target == PartnershipStatus.Approved && p.Creator.Status != CreatorStatus.Active) return Problem(400, "An inactive creator cannot be approved.");
        var old = p.Status; var now = DateTime.UtcNow;
        try { switch (target) { case PartnershipStatus.Approved when old == PartnershipStatus.Pending: p.Approve(now, u.UserAccountId!.Value, p.StartDateUtc, p.EndDateUtc); break; case PartnershipStatus.Approved when old == PartnershipStatus.Approved: p.ActivatePromotion(now, u.UserAccountId!.Value); break; case PartnershipStatus.Approved: p.Reactivate(now, u.UserAccountId!.Value); break; case PartnershipStatus.Rejected: p.Reject(now, u.UserAccountId!.Value, reason ?? "Rejected by merchant"); break; case PartnershipStatus.Suspended: p.Suspend(now, u.UserAccountId!.Value, reason ?? "Suspended by merchant"); await SuspendPromotion(p.Id, db, now, ct); break; case PartnershipStatus.Revoked: p.Revoke(now, u.UserAccountId!.Value); break; case PartnershipStatus.Blocked: p.Block(now, u.UserAccountId!.Value); break; default: throw new InvalidOperationException("Unsupported transition."); } if (target == PartnershipStatus.Approved && old != PartnershipStatus.Pending) await EnsurePromotion(p, u.UserAccountId!.Value, db, commissions, campaignOptions.Value, now, ct); }
        catch (CommissionConfigurationException ex) { db.ChangeTracker.Clear(); return Problem(409, $"Advertising cannot be activated until Platform commission settings are configured. {ex.Message}"); }
        catch (InvalidOperationException ex) { db.MerchantAuditEvents.Add(new MerchantAuditEvent { Id = Guid.NewGuid(), MerchantId = p.MerchantId, ActorUserAccountId = u.UserAccountId, EventType = "PartnershipInvalidTransitionAttempt", Detail = $"{old} -> {target}: {ex.Message}", CreatedAtUtc = now }); await db.SaveChangesAsync(ct); return Problem(409, ex.Message); }
        Audit(db, p, u.UserAccountId!.Value, old, target, target == PartnershipStatus.Approved && old == PartnershipStatus.Approved ? "PartnershipPromotionActivated" : $"Partnership{target}", reason, h, now);
        if (target is PartnershipStatus.Approved or PartnershipStatus.Rejected)
            await NotifyCreator(notifications, db, p.CreatorId, target == PartnershipStatus.Approved ? NotificationType.PartnershipApproved : NotificationType.PartnershipRejected, $"partnership:{p.Id}:merchant:{target}", target == PartnershipStatus.Approved ? "Promotion request approved" : "Promotion request declined", $"{p.Merchant.TradingName} has {(target == PartnershipStatus.Approved ? "approved" : "declined")} your promotion request.", target == PartnershipStatus.Approved ? "/?view=ads" : "/?view=find", h.TraceIdentifier, p.Id, ct);
        await db.SaveChangesAsync(ct); return Results.Ok(Item(p) with { PromotionActive = target == PartnershipStatus.Approved });
    }

    private static async Task<IResult> CreatorRevoke(Guid id, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct, string eventType)
    { var p = await db.MerchantCreatorPartnerships.Include(x => x.Merchant).Include(x => x.Creator).FirstOrDefaultAsync(x => x.Id == id && x.CreatorId == u.CreatorId, ct); if (p is null) return NotFound(); if (eventType == "PartnershipWithdrawn" && p.Status != PartnershipStatus.Pending) return Problem(409, "Only a pending request can be withdrawn."); if (eventType == "CreatorStoppedPromoting" && p.Status != PartnershipStatus.Approved) return Problem(409, "Only an approved partnership can be stopped."); var old = p.Status; var now = DateTime.UtcNow; p.Revoke(now, u.UserAccountId!.Value); Audit(db, p, u.UserAccountId.Value, old, PartnershipStatus.Revoked, eventType, null, h, now); await db.SaveChangesAsync(ct); return Results.Ok(Item(p)); }

    private static async Task<IResult> SetDates(Guid id, PartnershipDatesRequest r, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct)
    { var p = await db.MerchantCreatorPartnerships.FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct); if (p is null) return NotFound(); try { p.SetDates(r.StartDateUtc, r.EndDateUtc, DateTime.UtcNow, u.UserAccountId!.Value); } catch (ArgumentException ex) { return Problem(400, ex.Message); } AddMerchantAudit(db, p, "PartnershipDatesChanged", u.UserAccountId, r.ToString()); await db.SaveChangesAsync(ct); return Results.Ok(Item(p)); }
    private static async Task<IResult> SetLocations(Guid id, PartnershipLocationsRequest r, ICurrentUserService u, ApplicationDbContext db, HttpContext h, CancellationToken ct)
    { var p = await db.MerchantCreatorPartnerships.Include(x => x.Locations).FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == u.MerchantId, ct); if (p is null) return NotFound(); var ids = r.LocationIds.Distinct().ToArray(); var valid = await db.MerchantLocations.Where(x => x.MerchantId == u.MerchantId && x.IsActive && ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct); if (valid.Count != ids.Length) { AddMerchantAudit(db, p, "PartnershipCrossMerchantLocationAttempt", u.UserAccountId, null); await db.SaveChangesAsync(ct); return Problem(400, "Every location must be active and belong to this merchant."); } var now = DateTime.UtcNow; foreach (var old in p.Locations) old.IsActive = ids.Contains(old.MerchantLocationId); foreach (var add in ids.Where(x => p.Locations.All(y => y.MerchantLocationId != x))) p.Locations.Add(NewLocation(p.Id, add, u.UserAccountId!.Value, now)); AddMerchantAudit(db, p, "PartnershipLocationsChanged", u.UserAccountId, string.Join(',', ids)); await db.SaveChangesAsync(ct); return Results.Ok(Item(p)); }

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

    private static IQueryable<MerchantCreatorPartnership> Query(ApplicationDbContext db) => db.MerchantCreatorPartnerships.AsNoTracking().Include(x => x.Merchant).Include(x => x.Creator).Include(x => x.Locations).ThenInclude(x => x.MerchantLocation).OrderByDescending(x => x.RequestedAtUtc);
    private static async Task<IReadOnlyList<PartnershipListItem>> ItemsWithInitiator(IQueryable<MerchantCreatorPartnership> query, ApplicationDbContext db, CancellationToken ct)
    { var items = await query.ToListAsync(ct); var requesterIds = items.Select(x => x.RequestedByUserId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray(); var businessRequesters = await db.UserAccounts.AsNoTracking().Where(x => requesterIds.Contains(x.Id) && x.MerchantId != null).Select(x => x.Id).ToListAsync(ct); var now = DateTime.UtcNow; var ids = items.Select(x => x.Id).ToArray(); var active = (await db.CreatorMerchantCampaigns.AsNoTracking().Where(x => ids.Contains(x.MerchantCreatorPartnershipId) && x.Status == CampaignStatus.Active && x.StartsAtUtc <= now && x.ExpiresAtUtc > now).Select(x => x.MerchantCreatorPartnershipId).Distinct().ToListAsync(ct)).ToHashSet(); return items.Select(x => Item(x) with { InitiatedBy = x.RequestedByUserId.HasValue && businessRequesters.Contains(x.RequestedByUserId.Value) ? "Business" : "Creator", PromotionActive = active.Contains(x.Id) && x.IsTransactionEligibleAt(now) }).ToList(); }
    private static PartnershipListItem Item(MerchantCreatorPartnership p) => Item(p, p.Merchant, p.Creator);
    private static PartnershipListItem Item(MerchantCreatorPartnership p, Merchant m, Creator c) => new(p.Id, p.MerchantId, m.TradingName, p.CreatorId, c.DisplayName, p.Status, p.RequestedAtUtc, p.StartDateUtc, p.EndDateUtc, p.IntroductoryMessage, p.Locations.Where(x => x.IsActive).Select(x => new LocationItem(x.MerchantLocationId, x.MerchantLocation?.Name ?? "Location", x.MerchantLocation?.IsActive ?? true)).ToList(), ActivatedAtUtc: p.ApprovedAtUtc, ExpiresAtUtc: p.EndDateUtc);
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
