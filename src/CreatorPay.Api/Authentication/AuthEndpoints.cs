using CreatorPay.Application.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPay.Api.Authentication;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");
        group.MapPost("/login", async (LoginRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.LoginAsync(r, Context(h), ct))).RequireRateLimiting("auth-sensitive");
        group.MapPost("/refresh", async (RefreshRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.RefreshAsync(r.RefreshToken, Context(h), ct))).RequireRateLimiting("auth-sensitive");
        group.MapPost("/logout", async (TokenRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.LogoutAsync(r.RefreshToken, Context(h), ct)));
        group.MapPost("/logout-all", async (HttpContext h, ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.LogoutAllAsync(u.UserAccountId!.Value, Context(h), ct))).RequireAuthorization("AuthenticatedUser");
        group.MapGet("/me", async (ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.GetCurrentUserAsync(u.UserAccountId!.Value, ct))).RequireAuthorization("AuthenticatedUser");
        group.MapPost("/change-password", async (ChangePasswordRequest r, HttpContext h, ICurrentUserService u, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.ChangePasswordAsync(u.UserAccountId!.Value, r, Context(h), ct))).RequireAuthorization("AuthenticatedUser");
        group.MapPost("/forgot-password", async (ForgotPasswordRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => { await s.ForgotPasswordAsync(r.Email, Context(h), ct); return Results.Ok(new { message = "If the account is eligible, password reset instructions will be sent." }); }).RequireRateLimiting("auth-sensitive");
        group.MapPost("/reset-password", async (ResetPasswordRequest r, HttpContext h, IAuthenticationService s, CancellationToken ct) => ToHttp(await s.ResetPasswordAsync(r, Context(h), ct))).RequireRateLimiting("auth-sensitive");
        return endpoints;
    }
    private static RequestContext Context(HttpContext h) => new(h.Connection.RemoteIpAddress?.ToString(), h.Request.Headers.UserAgent.ToString(), h.TraceIdentifier);
    private static IResult ToHttp<T>(Result<T> result) => result.Succeeded ? Results.Ok(result.Value) : Problem(result.Error!);
    private static IResult ToHttp(OperationResult result) => result.Succeeded ? Results.Ok(new { succeeded = true }) : Problem(result.Error!);
    private static IResult Problem(string detail) => Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest, title: "Authentication request failed");
}
