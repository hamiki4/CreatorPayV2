using CreatorPay.Application.Authentication;
using CreatorPay.Application.Creators;
using CreatorPay.Api.Integration;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using CreatorPay.Infrastructure.Integration;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Api.Creators;

public static class CreatorEndpoints
{
    public static IEndpointRouteBuilder MapCreatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/creators").WithTags("Creators");
        group.MapPost("/register", async (RegisterCreatorRequest request, ICreatorService service, CancellationToken ct) => ToHttp(await service.RegisterAsync(request, ct), StatusCodes.Status201Created)).RequireRateLimiting("auth-sensitive");
        group.MapPost("/verify-email", async (VerifyCreatorRequest request, ICreatorService service, CancellationToken ct) => ToHttp(await service.VerifyEmailAsync(request.Token, ct))).RequireRateLimiting("auth-sensitive");
        group.MapPost("/verify-phone", async (VerifyCreatorRequest request, ICreatorService service, CancellationToken ct) => ToHttp(await service.VerifyPhoneAsync(request.Token, ct))).RequireRateLimiting("auth-sensitive");
        group.MapGet("/me", async (ICurrentUserService user, ICreatorService service, CancellationToken ct) => ToHttp(await service.GetMeAsync(user.UserAccountId!.Value, ct))).RequireAuthorization("CreatorOnboarding");
        group.MapPut("/me", async (UpdateCreatorProfileRequest request, HttpContext context,
            ICurrentUserService user, ICreatorService service, CancellationToken ct) =>
            context.User.HasClaim(ExternalProductAuthentication.SourceClaim, "V3External")
                ? Results.Problem("V3 account identity cannot be changed from Creator profile editing.",
                    statusCode: StatusCodes.Status403Forbidden)
                : ToHttp(await service.UpdateMeAsync(user.UserAccountId!.Value, request, ct)))
            .RequireAuthorization("CreatorOnboarding");
        group.MapPost("/me/profile-photo", async (HttpRequest request, ICurrentUserService user, ICreatorService service, CancellationToken ct) =>
        {
            var requestSize = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (requestSize is { IsReadOnly: false }) requestSize.MaxRequestBodySize = ProfilePhotoLimits.MaximumUploadBytes;
            var form = await request.ReadFormAsync(ct);
            var photo = form.Files.GetFile("photo") ?? form.Files.FirstOrDefault();
            if (photo is null || photo.Length == 0) return Problem("A profile photo file is required.");
            await using var stream = photo.OpenReadStream();
            return ToHttp(await service.UploadProfilePhotoAsync(user.UserAccountId!.Value, stream, photo.ContentType ?? "application/octet-stream", photo.Length, ct));
        }).RequireAuthorization("CreatorOnboarding").DisableAntiforgery().WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = ProfilePhotoLimits.MaximumUploadBytes }, new RequestSizeLimitAttribute(ProfilePhotoLimits.MaximumUploadBytes));
        group.MapDelete("/me/profile-photo", async (ICurrentUserService user, ICreatorService service, CancellationToken ct) => ToHttp(await service.RemoveProfilePhotoAsync(user.UserAccountId!.Value, ct))).RequireAuthorization("CreatorOnboarding");
        group.MapGet("/pending", async (ICreatorService service, CancellationToken ct) => Results.Ok(await service.GetPendingAsync(ct))).RequireAuthorization("AdminOperationsOnly");
        group.MapGet("/{creatorId:guid}", async (Guid creatorId, ICreatorService service, CancellationToken ct) => ToHttp(await service.GetAsync(creatorId, ct))).RequireAuthorization("AdminOperationsOnly");
        group.MapPost("/approve", async (CreatorDecisionRequest request, ICurrentUserService user, ICreatorService service, ExternalIntegrationService integration, CancellationToken ct) =>
        { var result = await service.ApproveAsync(request.CreatorId, user.UserAccountId!.Value, ct); var synchronized = await integration.SynchronizeLifecycleAsync(UserRole.Creator, request.CreatorId, "ACTIVE", ct); return synchronized && !result.Succeeded ? Results.Ok(new { succeeded = true }) : ToHttp(result); }).RequireAuthorization("AdminOperationsOnly");
        group.MapPost("/reject", async (CreatorDecisionRequest request, ICurrentUserService user, ICreatorService service, ExternalIntegrationService integration, CancellationToken ct) =>
        { var result = await service.RejectAsync(request.CreatorId, user.UserAccountId!.Value, request.Reason, ct); var synchronized = await integration.SynchronizeLifecycleAsync(UserRole.Creator, request.CreatorId, "REJECTED", ct); return synchronized && !result.Succeeded ? Results.Ok(new { succeeded = true }) : ToHttp(result); }).RequireAuthorization("AdminOperationsOnly");
        group.MapPost("/request-correction", async (CreatorDecisionRequest request, ICurrentUserService user, ICreatorService service, ExternalIntegrationService integration, CancellationToken ct) =>
        { var result = await service.RequestCorrectionAsync(request.CreatorId, user.UserAccountId!.Value, request.Reason, ct); var synchronized = await integration.SynchronizeLifecycleAsync(UserRole.Creator, request.CreatorId, "CORRECTION_REQUESTED", ct); return synchronized && !result.Succeeded ? Results.Ok(new { succeeded = true }) : ToHttp(result); }).RequireAuthorization("AdminOperationsOnly");
        group.MapPost("/suspend", async (CreatorDecisionRequest request, ICurrentUserService user, ICreatorService service, CancellationToken ct) => ToHttp(await service.SuspendAsync(request.CreatorId, user.UserAccountId!.Value, request.Reason, ct))).RequireAuthorization("AdminOperationsOnly");
        group.MapPost("/reactivate", async (CreatorDecisionRequest request, ICurrentUserService user, ICreatorService service, CancellationToken ct) => ToHttp(await service.ReactivateAsync(request.CreatorId, user.UserAccountId!.Value, request.Reason, ct))).RequireAuthorization("AdminOperationsOnly");
        return endpoints;
    }
    private static IResult ToHttp(CreatorResult result) => result.Succeeded ? Results.Ok(new { succeeded = true }) : Problem(result.Error!);
    private static IResult ToHttp<T>(CreatorResult<T> result, int successStatus = StatusCodes.Status200OK) => result.Succeeded ? Results.Json(result.Value, statusCode: successStatus) : Problem(result.Error!);
    private static IResult Problem(string detail) => Results.Problem(detail, statusCode: detail.Contains("already registered", StringComparison.OrdinalIgnoreCase) ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest, title: "Creator request failed");
}
