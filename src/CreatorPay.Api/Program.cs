using System.Text;
using System.Net;
using System.Threading.RateLimiting;
using CreatorPay.Api.Authentication;
using CreatorPay.Api.Admin;
using CreatorPay.Api.Commission;
using CreatorPay.Api.Creators;
using CreatorPay.Api.CustomerVerification;
using CreatorPay.Api.Earnings;
using CreatorPay.Api.Merchants;
using CreatorPay.Api.Notifications;
using CreatorPay.Api.Operations;
using CreatorPay.Api.Organization;
using CreatorPay.Api.Partnerships;
using CreatorPay.Api.Qr;
using CreatorPay.Api.Risk;
using CreatorPay.Api.Wallet;
using CreatorPay.Api.OfflineSync;
using CreatorPay.Api.Reporting;
using CreatorPay.Application;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Operations;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
ProductionConfiguration.Validate(builder.Configuration, builder.Environment);
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
var rateLimits = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new();
var cors = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new();
var reverseProxy = builder.Configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new();

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "O"; });
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c => c.ProblemDetails.Extensions["correlationId"] = c.HttpContext.TraceIdentifier);
builder.Services.AddHttpContextAccessor(); builder.Services.AddResponseCompression();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var address in reverseProxy.KnownProxies) o.KnownProxies.Add(IPAddress.Parse(address));
});
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<HealthOptions>(builder.Configuration.GetSection(HealthOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.AddCors(o => o.AddPolicy("Web", p => { if (cors.AllowedOrigins.Length > 0) p.WithOrigins(cors.AllowedOrigins).AllowAnyHeader().AllowAnyMethod(); }));
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); builder.Services.AddApplication(); builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]).AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready", "database"]);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30) });
builder.Services.AddAuthorization(o => { o.AddPolicy("AuthenticatedUser", p => p.RequireAuthenticatedUser()); foreach (var role in Enum.GetValues<UserRole>()) o.AddPolicy($"{role}Only", p => p.RequireRole(role.ToString())); o.AddPolicy("MerchantOperations", p => p.RequireRole(nameof(UserRole.MerchantAdmin), nameof(UserRole.Supervisor), nameof(UserRole.Cashier))); });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (c, ct) => { CreatorPayTelemetry.RateLimitViolations.Add(1); c.HttpContext.Response.ContentType = "application/problem+json"; await c.HttpContext.Response.WriteAsJsonAsync(new { type = "https://httpstatuses.com/429", title = "Too many requests", status = 429, correlationId = c.HttpContext.TraceIdentifier }, ct); };
    o.AddPolicy("auth-sensitive", h => RateLimitPartition.GetFixedWindowLimiter($"{h.Connection.RemoteIpAddress}:{h.Request.Path}", _ => new() { PermitLimit = rateLimits.AuthPermitLimit, Window = TimeSpan.FromSeconds(rateLimits.WindowSeconds), QueueLimit = 0 }));
    o.AddPolicy("financial-sensitive", h => RateLimitPartition.GetFixedWindowLimiter($"{h.User.FindFirst("merchant_id")?.Value ?? h.Connection.RemoteIpAddress?.ToString()}:{h.Request.Path}", _ => new() { PermitLimit = rateLimits.FinancialPermitLimit, Window = TimeSpan.FromSeconds(rateLimits.WindowSeconds), QueueLimit = 0 }));
    o.AddPolicy("admin-report", h => RateLimitPartition.GetFixedWindowLimiter($"{h.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}:{h.Request.Path}", _ => new() { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.Logger.LogInformation("CreatorPay API starting in {Environment}; version {Version}", app.Environment.EnvironmentName, typeof(Program).Assembly.GetName().Version?.ToString());
app.UseForwardedHeaders(); if (!app.Environment.IsDevelopment()) app.UseHsts();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseExceptionHandler(); app.UseResponseCompression(); app.UseMiddleware<RequestContextMiddleware>();
app.Use(async (context, next) => { context.Response.Headers.XContentTypeOptions = "nosniff"; context.Response.Headers.XFrameOptions = "DENY"; context.Response.Headers["Referrer-Policy"] = "no-referrer"; context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()"; context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"; context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-site"; context.Response.Headers["Cache-Control"] = "no-store"; await next(); });
app.UseHttpsRedirection(); app.UseCors("Web"); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = x => x.Tags.Contains("live"), ResponseWriter = WriteHealth });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready"), ResponseWriter = WriteHealth });
app.MapGet("/health", () => Results.Redirect("/health/live")).ExcludeFromDescription();
app.MapAuthEndpoints(); app.MapCreatorEndpoints(); app.MapMerchantEndpoints(); app.MapOrganizationEndpoints(); app.MapPartnershipEndpoints(); app.MapQrEndpoints(); app.MapCommissionEndpoints(); app.MapWalletEndpoints(); app.MapOfflineSyncEndpoints(); app.MapEarningsEndpoints(); app.MapCustomerVerificationEndpoints(); app.MapNotificationEndpoints(); app.MapRiskEndpoints();
app.MapAdminEndpoints();
app.MapReportingEndpoints();
app.Run();

static Task WriteHealth(HttpContext context, HealthReport report) { context.Response.ContentType = "application/json"; return context.Response.WriteAsJsonAsync(new { status = report.Status.ToString(), service = "CreatorPay API", correlationId = context.TraceIdentifier }); }
public partial class Program;
