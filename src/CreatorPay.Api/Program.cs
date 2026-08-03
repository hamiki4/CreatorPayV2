using System.Text;
using System.Threading.RateLimiting;
using CreatorPay.Api.Authentication;
using CreatorPay.Api.Creators;
using CreatorPay.Api.Merchants;
using CreatorPay.Api.Organization;
using CreatorPay.Api.Partnerships;
using CreatorPay.Api.Qr;
using CreatorPay.Application;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Authentication JWT issuer, audience, and a signing key of at least 32 characters are required.");

builder.Services.AddOpenApi(); builder.Services.AddProblemDetails(); builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); builder.Services.AddApplication(); builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30) });
builder.Services.AddAuthorization(o => { o.AddPolicy("AuthenticatedUser", p => p.RequireAuthenticatedUser()); foreach (var role in Enum.GetValues<UserRole>()) o.AddPolicy($"{role}Only", p => p.RequireRole(role.ToString())); o.AddPolicy("MerchantOperations", p => p.RequireRole(nameof(UserRole.MerchantAdmin), nameof(UserRole.Supervisor), nameof(UserRole.Cashier))); });
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = StatusCodes.Status429TooManyRequests; o.AddPolicy("auth-sensitive", h => RateLimitPartition.GetFixedWindowLimiter(h.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });

var app = builder.Build(); if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseExceptionHandler(); app.UseHttpsRedirection(); app.Use(async (context, next) => { context.Response.Headers.XContentTypeOptions = "nosniff"; context.Response.Headers.XFrameOptions = "DENY"; context.Response.Headers["Referrer-Policy"] = "no-referrer"; await next(); });
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "CreatorPay API" })).WithName("GetHealth").WithTags("Health"); app.MapAuthEndpoints(); app.MapCreatorEndpoints(); app.MapMerchantEndpoints(); app.MapOrganizationEndpoints(); app.MapPartnershipEndpoints(); app.MapQrEndpoints(); app.Run();
public partial class Program;
