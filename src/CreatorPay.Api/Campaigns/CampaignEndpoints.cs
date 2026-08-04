using CreatorPay.Application.Authentication;
using CreatorPay.Application.Campaigns;
namespace CreatorPay.Api.Campaigns;

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder e)
    {
        var c = e.MapGroup("/api/v1/creator/campaigns").RequireAuthorization("CreatorOnly");
        c.MapPost("/requests", (RequestCampaignRequest r, ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.RequestAsync(u.CreatorId!.Value, r, ct), 201)); c.MapGet("/", (ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.GetCreatorCampaignsAsync(u.CreatorId!.Value, ct))); c.MapGet("/{id:guid}", (Guid id, ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.GetAsync(id, u.CreatorId, null, ct))); c.MapPost("/{id:guid}/start", (Guid id, ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.StartAsync(u.CreatorId!.Value, u.UserAccountId!.Value, id, ct))); c.MapPost("/{id:guid}/renewal-request", (Guid id, RenewalRequest r, ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.RequestRenewalAsync(u.CreatorId!.Value, id, r, ct), 201));
        var m = e.MapGroup("/api/v1/merchant/campaigns").RequireAuthorization("MerchantAdminOnly");
        m.MapGet("/requests", (ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.GetMerchantCampaignsAsync(u.MerchantId!.Value, true, ct))); m.MapGet("/", (ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.GetMerchantCampaignsAsync(u.MerchantId!.Value, false, ct))); m.MapPost("/{id:guid}/approve", (Guid id, ApproveCampaignRequest r, ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.ApproveAsync(u.MerchantId!.Value, u.UserAccountId!.Value, id, r, ct))); m.MapPost("/{id:guid}/reject", (Guid id, RejectCampaignRequest r, ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.RejectAsync(u.MerchantId!.Value, u.UserAccountId!.Value, id, r, ct))); m.MapPost("/{id:guid}/suspend", (Guid id, ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.SuspendAsync(u.MerchantId!.Value, u.UserAccountId!.Value, id, ct))); m.MapPost("/{id:guid}/cancel", (Guid id, ICurrentUserService u, ICampaignService s, CancellationToken ct) => Run(() => s.CancelAsync(u.MerchantId!.Value, u.UserAccountId!.Value, id, ct))); return e;
    }
    static async Task<IResult> Run<T>(Func<Task<T>> f, int status = 200) { try { return Results.Json(await f(), statusCode: status); } catch (KeyNotFoundException x) { return Results.Problem(statusCode: 404, detail: x.Message); } catch (ArgumentException x) { return Results.Problem(statusCode: 400, detail: x.Message); } catch (InvalidOperationException x) { return Results.Problem(statusCode: 409, detail: x.Message); } }
}
