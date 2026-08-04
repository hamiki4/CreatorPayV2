using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Qr;
using CreatorPay.Domain.Entities;
using CreatorPay.Infrastructure.Persistence;

namespace CreatorPay.Api.Qr;

public sealed record RegenerateQrRequest(bool Confirmed);
public sealed record RevokeQrRequest(string? Reason);

public static class QrEndpoints
{
    public static IEndpointRouteBuilder MapQrEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var creator = endpoints.MapGroup("/api/v1/creator/qr").WithTags("Creator QR").RequireAuthorization("CreatorOnly");
        creator.MapGet("/", Current);
        creator.MapGet("/image", Image);
        creator.MapPost("/regenerate", Regenerate);
        creator.MapPost("/revoke", Revoke);
        creator.MapGet("/history", History);
        endpoints.MapPost("/api/v1/merchant/qr/validate", Validate).WithTags("Merchant QR").RequireAuthorization("MerchantOperations");
        return endpoints;
    }

    private static async Task<IResult> Current(ICurrentUserService user, ICreatorQrService service, CancellationToken ct)
    { try { return Results.Ok(await service.IssueOrGetAsync(user.CreatorId!.Value, user.UserAccountId!.Value, ct)); } catch (InvalidOperationException e) { return Problem(403, e.Message); } }
    private static async Task<IResult> Image(ICurrentUserService user, ICreatorQrService service, IQrImageGenerator images, ApplicationDbContext db, CancellationToken ct)
    { try { var qr = await service.IssueOrGetAsync(user.CreatorId!.Value, user.UserAccountId!.Value, ct); db.CreatorAuditEvents.Add(new CreatorAuditEvent { Id = Guid.NewGuid(), CreatorId = user.CreatorId.Value, ActorUserAccountId = user.UserAccountId, EventType = "CreatorQrImageDownloaded", Detail = $"PublicQrId={qr.PublicQrId}", CreatedAtUtc = DateTime.UtcNow, CreatedBy = user.UserAccountId.ToString() }); await db.SaveChangesAsync(ct); return Results.File(images.GeneratePng(qr.Payload), "image/png", "creatorpay-qr.png"); } catch (InvalidOperationException e) { return Problem(403, e.Message); } }
    private static async Task<IResult> Regenerate(RegenerateQrRequest request, ICurrentUserService user, ICreatorQrService service, CancellationToken ct)
    { try { return Results.Ok(await service.RegenerateAsync(user.CreatorId!.Value, user.UserAccountId!.Value, request.Confirmed, ct)); } catch (ArgumentException e) { return Problem(400, e.Message); } catch (InvalidOperationException e) { return Problem(403, e.Message); } }
    private static async Task<IResult> Revoke(RevokeQrRequest request, ICurrentUserService user, ICreatorQrService service, CancellationToken ct)
    { try { await service.RevokeAsync(user.CreatorId!.Value, user.UserAccountId!.Value, request.Reason, ct); return Results.NoContent(); } catch (KeyNotFoundException) { return Problem(404, "No active QR code was found."); } }
    private static async Task<IResult> History(ICurrentUserService user, ICreatorQrService service, CancellationToken ct) => Results.Ok(await service.GetHistoryAsync(user.CreatorId!.Value, ct));
    private static async Task<IResult> Validate(MerchantQrValidationRequest request, ICurrentUserService user, ICreatorQrService service, CancellationToken ct)
    { if (user.MerchantId is null || user.UserAccountId is null) return Problem(403, "Merchant access is unavailable."); var result = await service.ValidateAsync(request.Payload, request.LocationId, user.MerchantId.Value, user.UserAccountId.Value, user.Role!, user.CashierId, user.SupervisorId, ct); return Results.Ok(result); }
    private static IResult Problem(int status, string detail) => Results.Problem(statusCode: status, title: "QR request failed", detail: detail);
}
