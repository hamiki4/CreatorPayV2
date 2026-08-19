using CreatorPay.Application.Authentication;
using CreatorPay.Application.Checkout;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Application.Operations;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace CreatorPay.Api.Checkout;

public static class CheckoutEndpoints
{
    public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder e)
    {
        e.MapPost("/api/v1/customers/register", (RegisterCustomerRequest r, ICheckoutService s, CancellationToken ct) => Run(async () =>
        {
            var id = await s.RegisterCustomerAsync(r, ct);
            return new CustomerRegistrationResponse(id, "Shopper account created. You can sign in now.", false);
        }, 201));
        var c = e.MapGroup("/api/v1/customer").RequireAuthorization("CustomerOnly");
        c.MapPost("/checkouts", (CreateCheckoutRequest r, HttpRequest h, ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.CreateAsync(u.CustomerId!.Value, Key(h), r, ct), 201));
        c.MapPost("/checkouts/by-offer", (CreateOfferCheckoutRequest r, HttpRequest h, ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.CreateFromOfferAsync(u.CustomerId!.Value, Key(h), r, ct), 201));
        c.MapGet("/checkouts", (ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.GetCustomerCheckoutsAsync(u.CustomerId!.Value, ct)));
        c.MapPost("/checkouts/{id:guid}/approve", async (Guid id, HttpRequest h, ICurrentUserService u, ICheckoutService s, IHubContext<CheckoutHub> hub, CancellationToken ct) => { try { var x = await s.ApproveAsync(u.CustomerId!.Value, id, Key(h), ct); await hub.Clients.Group($"merchant:{x.MerchantId}").SendAsync("CheckoutCompleted", x, ct); return Results.Ok(x); } catch (KeyNotFoundException x) { return Results.Problem(statusCode: 404, detail: x.Message); } catch (UnauthorizedAccessException x) { return Results.Problem(statusCode: 403, detail: x.Message); } catch (ArgumentException x) { return Results.Problem(statusCode: 400, detail: x.Message); } catch (InvalidOperationException x) { return Results.Problem(statusCode: 409, detail: x.Message); } });
        c.MapPost("/checkouts/{id:guid}/reject", (Guid id, ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.RejectAsync(u.CustomerId!.Value, id, ct)));
        c.MapGet("/wallet", (ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.GetWalletAsync(u.CustomerId!.Value, ct)));
        c.MapGet("/profile", async (ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => await Run(async () => await (from account in db.UserAccounts.AsNoTracking() join customer in db.Customers.AsNoTracking() on account.CustomerId equals customer.Id where account.Id == u.UserAccountId && customer.Id == u.CustomerId select new CustomerProfileDto(customer.DisplayName, account.Email, customer.PhoneNumber, account.Status.ToString(), account.IsEmailVerified, account.IsPhoneVerified)).SingleAsync(ct)));
        c.MapPost("/cashback-payouts", (HttpRequest h, ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.RequestPayoutAsync(u.CustomerId!.Value, Key(h), ct), 201));
        c.MapGet("/cashback-payouts", (ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.GetPayoutsAsync(u.CustomerId, ct)));
        var cashier = e.MapGroup("/api/v1/cashier/checkouts").RequireAuthorization("CashierOnly");
        cashier.MapPost("/present", async (PresentCheckoutRequest r, HttpRequest h, ICurrentUserService u, ICheckoutService s, IHubContext<CheckoutHub> hub, CancellationToken ct) => { try { var x = await s.PresentAsync(u.MerchantId!.Value, u.CashierId!.Value, Key(h), r, ct); await hub.Clients.Group($"customer:{x.CustomerId}").SendAsync("CheckoutApprovalRequired", x, ct); return Results.Ok(x); } catch (KeyNotFoundException x) { return Results.Problem(statusCode: 404, detail: x.Message); } catch (UnauthorizedAccessException x) { return Results.Problem(statusCode: 403, detail: x.Message); } catch (ArgumentException x) { return Results.Problem(statusCode: 400, detail: x.Message); } catch (InvalidOperationException x) { return Results.Problem(statusCode: 409, detail: x.Message); } });
        cashier.MapPost("/offer", SubmitCashierOffer);
        cashier.MapPost("/validate-offer", ValidateCashierOffer);
        cashier.MapPost("/by-creator", SubmitCashierCreator);
        cashier.MapPost("/validate-creator", ValidateCashierCreator);
        cashier.MapPost("/repeat-use-approvals", CreateRepeatUseApproval);
        var repeat = e.MapGroup("/api/v1/merchant/repeat-use-approvals").RequireAuthorization("AuthenticatedUser");
        repeat.MapPost("/{id:guid}/approve", (Guid id, DecideRepeatUseApprovalRequest r, HttpContext h, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => DecideRepeatUseApproval(id, true, r, h, u, db, ct));
        repeat.MapPost("/{id:guid}/reject", (Guid id, DecideRepeatUseApprovalRequest r, HttpContext h, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => DecideRepeatUseApproval(id, false, r, h, u, db, ct));
        var a = e.MapGroup("/api/v1/admin/customer-payouts").RequireAuthorization("PlatformAdminOnly");
        a.MapGet("/", (ICheckoutService s, CancellationToken ct) => Run(() => s.GetPayoutsAsync(null, ct)));
        a.MapPost("/{id:guid}/processing", (Guid id, ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.MarkPayoutProcessingAsync(id, u.UserAccountId!.Value, ct)));
        a.MapPost("/{id:guid}/mark-paid", (Guid id, MarkCustomerPayoutPaidRequest r, HttpRequest h, ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.MarkPayoutPaidAsync(id, u.UserAccountId!.Value, Key(h), r, ct)));
        a.MapPost("/{id:guid}/mark-failed", (Guid id, string reason, ICurrentUserService u, ICheckoutService s, CancellationToken ct) => Run(() => s.MarkPayoutFailedAsync(id, u.UserAccountId!.Value, reason, ct)));
        return e;
    }
    static string Key(HttpRequest r) => r.Headers["Idempotency-Key"].ToString();
    static async Task<IResult> SubmitCashierOffer(CashierOfferCheckoutRequest r, HttpRequest h, ICurrentUserService u, ICheckoutService s, ApplicationDbContext db, IOptions<PilotOptions> pilot, CancellationToken ct)
    {
        if (pilot.Value.Enabled && r.PurchaseAmount > pilot.Value.MaximumPurchaseAmount) return Results.Problem(statusCode: 409, detail: "Pilot maximum purchase amount exceeded.");
        var locationId = await db.CashierLocationAssignments.AsNoTracking().Where(x => x.CashierId == u.CashierId && x.IsActive).OrderByDescending(x => x.IsPrimary).Select(x => (Guid?)x.MerchantLocationId).FirstOrDefaultAsync(ct);
        if (!locationId.HasValue) return Results.Problem(statusCode: 403, detail: "Cashier has no active Business location assignment.");
        return await Run(() => s.SubmitOfferAsync(u.MerchantId!.Value, u.CashierId!.Value, u.UserAccountId!.Value, Key(h), new(r.QrPayload, locationId.Value, r.ShopperPhoneNumber, r.PurchaseAmount), ct));
    }
    static async Task<IResult> ValidateCashierOffer(CashierQrValidationRequest r, ICurrentUserService u, ICheckoutService s, ApplicationDbContext db, CancellationToken ct)
    {
        var locationId = await db.CashierLocationAssignments.AsNoTracking().Where(x => x.CashierId == u.CashierId && x.IsActive).OrderByDescending(x => x.IsPrimary).Select(x => (Guid?)x.MerchantLocationId).FirstOrDefaultAsync(ct);
        if (!locationId.HasValue) return Results.Problem(statusCode: 403, detail: "Cashier has no active Business location assignment.");
        return await Run(() => s.ValidateOfferAsync(u.MerchantId!.Value, u.CashierId!.Value, locationId.Value, r.QrPayload, ct));
    }
    static async Task<IResult> SubmitCashierCreator(CashierCreatorCheckoutRequest r, HttpRequest h, ICurrentUserService u, ICheckoutService s, ApplicationDbContext db, IOptions<PilotOptions> pilot, CancellationToken ct)
    {
        if (pilot.Value.Enabled && r.PurchaseAmount > pilot.Value.MaximumPurchaseAmount) return Results.Problem(statusCode: 409, detail: "Pilot maximum purchase amount exceeded.");
        return await Run(async () =>
        {
            var payload = await CreatorPayload(r.CreatorCode, db, ct);
            var locationId = await ActiveCashierLocation(u, db, ct);
            return await s.SubmitOfferAsync(u.MerchantId!.Value, u.CashierId!.Value, u.UserAccountId!.Value, Key(h), new(payload, locationId, r.ShopperPhoneNumber, r.PurchaseAmount), ct);
        });
    }
    static async Task<IResult> ValidateCashierCreator(CashierCreatorValidationRequest r, ICurrentUserService u, ICheckoutService s, ApplicationDbContext db, CancellationToken ct) =>
        await Run(async () => await s.ValidateOfferAsync(u.MerchantId!.Value, u.CashierId!.Value, await ActiveCashierLocation(u, db, ct), await CreatorPayload(r.CreatorCode, db, ct), ct));
    static Task<Guid?> ActiveCashierLocation(ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) =>
        db.CashierLocationAssignments.AsNoTracking().Where(x => x.CashierId == u.CashierId && x.IsActive).OrderByDescending(x => x.IsPrimary).Select(x => (Guid?)x.MerchantLocationId).FirstOrDefaultAsync(ct);
    static async Task<string> CreatorPayload(string creatorCode, ApplicationDbContext db, CancellationToken ct)
    {
        var code = (creatorCode ?? string.Empty).Trim();
        if (code.Length != 4 || code[0] < '1' || code[0] > '9' || code.Skip(1).Any(x => x < '0' || x > '9')) throw new KeyNotFoundException("Creator ID not found.");
        if (!await db.Creators.AsNoTracking().AnyAsync(x => x.CreatorCode == code, ct)) throw new KeyNotFoundException("Creator ID not found.");
        return $"creatorpay:creator:{code}";
    }
    static async Task<IResult> CreateRepeatUseApproval(CreateRepeatUseApprovalRequest r, HttpContext h, ICurrentUserService u, ApplicationDbContext db, IPhoneNumberNormalizer phones, IOptions<CheckoutOptions> configured, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Reason)) return Results.BadRequest(new { error = "Reason is required." });
        var checkout = await db.CheckoutSessions.SingleOrDefaultAsync(x => x.Id == r.CheckoutId && x.MerchantId == u.MerchantId && x.CashierId == u.CashierId, ct);
        if (checkout is null) return Results.NotFound();
        var now = DateTime.UtcNow;
        if (checkout.Status != CheckoutSessionStatus.AwaitingCustomerApproval || checkout.ExpiresAtUtc <= now) return Results.Conflict(new { error = "Checkout is not eligible for an override." });
        var existing = await db.RepeatUseApprovalRequests.SingleOrDefaultAsync(x => x.QrReference == checkout.PublicCheckoutId && x.Status != RepeatUseApprovalStatus.Expired && x.Status != RepeatUseApprovalStatus.Cancelled && x.Status != RepeatUseApprovalStatus.Denied, ct);
        if (existing is not null) return Results.Conflict(new { error = "An active override request already exists." });
        var campaign = await db.CreatorMerchantCampaigns.SingleAsync(x => x.Id == checkout.CampaignId, ct);
        var shopper = await db.Customers.SingleAsync(x => x.Id == checkout.CustomerId, ct);
        var zoneId = await db.MerchantLocations.Where(x => x.Id == checkout.MerchantLocationId).Select(x => x.TimeZoneId).SingleAsync(ct);
        var local = TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        var request = new RepeatUseApprovalRequest { Id = Guid.NewGuid(), PublicApprovalRequestId = $"RUA-{Guid.NewGuid():N}", MerchantId = checkout.MerchantId, MerchantLocationId = checkout.MerchantLocationId!.Value, CreatorId = checkout.CreatorId, MerchantCreatorPartnershipId = campaign.MerchantCreatorPartnershipId, CashierId = checkout.CashierId!.Value, CreatedByUserId = u.UserAccountId!.Value, CustomerPhoneReferenceId = Guid.NewGuid(), CustomerPhoneHash = shopper.NormalizedPhoneNumber, MaskedPhoneNumber = phones.Mask(shopper.NormalizedPhoneNumber), MerchantLocalDate = DateOnly.FromDateTime(local), PurchaseAmount = checkout.PurchaseAmount!.Value, CurrencyCode = configured.Value.CurrencyCode, QrReference = checkout.PublicCheckoutId, Status = RepeatUseApprovalStatus.AwaitingSupervisorApproval, Reason = r.Reason.Trim(), RequestedAtUtc = now, ExpiresAtUtc = now.AddMinutes(configured.Value.RepeatUseApprovalLifetimeMinutes), IdempotencyKey = Key(h.Request), CorrelationId = h.TraceIdentifier, RequiresSupervisorApproval = true, CreatedAtUtc = now };
        db.Add(request); AddHistory(db, request, null, request.Status, u.UserAccountId, "Requested", request.Reason, now); AddAudit(db, "RepeatUseOverrideRequested", u.UserAccountId, request, checkout, now, h.TraceIdentifier); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/merchant/repeat-use-approvals/{request.Id}", Map(request, checkout.Id));
    }
    static async Task<IResult> DecideRepeatUseApproval(Guid id, bool approve, DecideRepeatUseApprovalRequest r, HttpContext h, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct)
    {
        if (u.Role is not (nameof(UserRole.Supervisor) or nameof(UserRole.MerchantAdmin))) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(r.Reason)) return Results.BadRequest(new { error = "Reason is required." });
        var request = await db.RepeatUseApprovalRequests.SingleOrDefaultAsync(x => x.Id == id, ct); if (request is null) return Results.NotFound();
        if (request.MerchantId != u.MerchantId) return Results.Forbid();
        if (u.Role == nameof(UserRole.Supervisor) && !await db.SupervisorLocationAssignments.AnyAsync(x => x.SupervisorId == u.SupervisorId && x.MerchantLocationId == request.MerchantLocationId && x.IsActive, ct)) return Results.Forbid();
        if (request.CreatedByUserId == u.UserAccountId) return Results.Forbid();
        var now = DateTime.UtcNow; if (request.ExpiresAtUtc <= now) { var old = request.Status; request.Status = RepeatUseApprovalStatus.Expired; AddHistory(db, request, old, request.Status, u.UserAccountId, "Expired", r.Reason.Trim(), now); AddAudit(db, "RepeatUseOverrideExpired", u.UserAccountId, request, null, now, h.TraceIdentifier); await db.SaveChangesAsync(ct); return Results.Conflict(new { error = "Override request expired." }); }
        if (request.Status != RepeatUseApprovalStatus.AwaitingSupervisorApproval) return Results.Conflict(new { error = "Override request has already been decided or consumed." });
        var previous = request.Status; request.Status = approve ? RepeatUseApprovalStatus.SupervisorApproved : RepeatUseApprovalStatus.Denied; request.SupervisorApproved = approve; request.SupervisorUserId = u.UserAccountId; request.SupervisorDecisionAtUtc = now; if (!approve) request.FinalizedAtUtc = now;
        AddHistory(db, request, previous, request.Status, u.UserAccountId, approve ? "Approved" : "Rejected", r.Reason.Trim(), now); AddAudit(db, approve ? "RepeatUseOverrideApproved" : "RepeatUseOverrideRejected", u.UserAccountId, request, null, now, h.TraceIdentifier); await db.SaveChangesAsync(ct); return Results.Ok(Map(request, Guid.Empty));
    }
    static RepeatUseApprovalDto Map(RepeatUseApprovalRequest x, Guid checkoutId) => new(x.Id, x.PublicApprovalRequestId, checkoutId, x.Status.ToString(), x.MaskedPhoneNumber, x.Reason, x.RequestedAtUtc, x.ExpiresAtUtc, x.SupervisorUserId, x.SupervisorDecisionAtUtc);
    static void AddHistory(ApplicationDbContext db, RepeatUseApprovalRequest x, RepeatUseApprovalStatus? old, RepeatUseApprovalStatus next, Guid? actor, string action, string reason, DateTime now) => db.RepeatUseApprovalHistories.Add(new() { Id = Guid.NewGuid(), RepeatUseApprovalRequestId = x.Id, PreviousStatus = old, NewStatus = next, ActorUserId = actor, Action = action, Reason = reason, OccurredAtUtc = now, CreatedAtUtc = now });
    static void AddAudit(ApplicationDbContext db, string type, Guid? actor, RepeatUseApprovalRequest x, CheckoutSession? checkout, DateTime now, string correlation) => db.OperationalAuditEvents.Add(new() { Id = Guid.NewGuid(), EventType = type, ActorUserId = actor, SubjectId = x.Id, MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { x.PublicApprovalRequestId, x.QrReference, CheckoutId = checkout?.Id, x.MerchantId, x.MerchantLocationId, x.CashierId, x.CreatorId, x.MerchantCreatorPartnershipId, x.MaskedPhoneNumber, x.Reason, x.ExpiresAtUtc }), CorrelationId = correlation, CreatedAtUtc = now });
    static async Task<IResult> Run<T>(Func<Task<T>> f, int status = 200) { try { return Results.Json(await f(), statusCode: status); } catch (KeyNotFoundException x) { return Results.Problem(statusCode: 404, detail: x.Message); } catch (UnauthorizedAccessException x) { return Results.Problem(statusCode: 403, detail: x.Message); } catch (ArgumentException x) { return Results.Problem(statusCode: 400, detail: x.Message); } catch (InvalidOperationException x) { return Results.Problem(statusCode: 409, detail: x.Message); } }
}
