using System.Text;
using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CreatorPay.Api.Authentication;
using CreatorPay.Api.Admin;
using CreatorPay.Api.Commission;
using CreatorPay.Api.Creators;
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
using CreatorPay.Api.Campaigns;
using CreatorPay.Api.Checkout;
using CreatorPay.Api.Discovery;
using CreatorPay.Api.Testing;
using CreatorPay.Api.Support;
using CreatorPay.Application;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.Creators;
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
if (builder.Environment.IsEnvironment("E2E")) rateLimits.AuthPermitLimit = Math.Max(rateLimits.AuthPermitLimit, 1000);
var cors = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new();
var featureFlags = builder.Configuration.GetSection(FeatureFlagOptions.SectionName).Get<FeatureFlagOptions>() ?? new();
var reverseProxy = builder.Configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>() ?? new();

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "O"; });
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c => c.ProblemDetails.Extensions["correlationId"] = c.HttpContext.TraceIdentifier);
builder.Services.AddHttpContextAccessor(); builder.Services.AddResponseCompression();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = ProfilePhotoLimits.MaximumUploadBytes);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var address in reverseProxy.KnownProxies) o.KnownProxies.Add(IPAddress.Parse(address));
});
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.Configure<HealthOptions>(builder.Configuration.GetSection(HealthOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.Configure<ErrorMonitoringOptions>(builder.Configuration.GetSection(ErrorMonitoringOptions.SectionName));
builder.Services.AddSingleton<IErrorMonitoringHook, LoggingErrorMonitoringHook>();
builder.Services.AddCors(o => o.AddPolicy("Web", p => { if (cors.AllowedOrigins.Length > 0) p.WithOrigins(cors.AllowedOrigins).AllowAnyHeader().AllowAnyMethod(); }));
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); builder.Services.AddApplication(); builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]).AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready", "database"]);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30) };
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (context.Principal?.FindFirst(AuthenticationClaimTypes.AccountStatus) is null || context.Principal.HasClaim("test_token", "true")) return;
            if (!Guid.TryParse(context.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) { context.Fail("Invalid access token."); return; }
            var store = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationStore>();
            var user = await store.FindUserAsync(userId, context.HttpContext.RequestAborted); var now = DateTime.UtcNow;
            if (user is null || !AuthenticationService.CanSignIn(user) || user.LockoutEndUtc > now) { context.Fail("Account is not eligible."); return; }
            if (context.Principal.Identity is ClaimsIdentity identity)
            {
                foreach (var type in new[] { AuthenticationClaimTypes.AccountStatus, AuthenticationClaimTypes.EmailVerified, AuthenticationClaimTypes.PhoneVerified }) foreach (var claim in identity.FindAll(type).ToArray()) identity.RemoveClaim(claim);
                identity.AddClaim(new(AuthenticationClaimTypes.AccountStatus, user.Status.ToString()));
                identity.AddClaim(new(AuthenticationClaimTypes.EmailVerified, user.IsEmailVerified.ToString().ToLowerInvariant()));
                identity.AddClaim(new(AuthenticationClaimTypes.PhoneVerified, user.IsPhoneVerified.ToString().ToLowerInvariant()));
            }
        }
    };
});
builder.Services.AddAuthorization(o =>
{
    static bool Status(ClaimsPrincipal user, params AccountStatus[] statuses) => statuses.Any(status => user.HasClaim(AuthenticationClaimTypes.AccountStatus, status.ToString()));
    o.AddPolicy("AuthenticatedUser", p => p.RequireAuthenticatedUser());
    foreach (var role in Enum.GetValues<UserRole>()) o.AddPolicy($"{role}Only", p => p.RequireAssertion(c => c.User.IsInRole(role.ToString()) && Status(c.User, AccountStatus.Active)));
    o.AddPolicy("AdminOperations", p => p.RequireAssertion(c => (c.User.IsInRole(nameof(UserRole.PlatformAdmin)) || c.User.IsInRole(nameof(UserRole.OperationsAdmin))) && Status(c.User, AccountStatus.Active)));
    o.AddPolicy("CreatorOnboarding", p => p.RequireAssertion(c => c.User.IsInRole(nameof(UserRole.Creator)) && Status(c.User, AccountStatus.PendingVerification, AccountStatus.PendingApproval, AccountStatus.Active)));
    o.AddPolicy("MerchantOnboarding", p => p.RequireAssertion(c => c.User.IsInRole(nameof(UserRole.MerchantAdmin)) && Status(c.User, AccountStatus.PendingVerification, AccountStatus.PendingApproval, AccountStatus.Active)));
    o.AddPolicy("MerchantOperations", p => { p.RequireAssertion(c => c.User.IsInRole(nameof(UserRole.MerchantAdmin)) || c.User.IsInRole(nameof(UserRole.Supervisor)) || c.User.IsInRole(nameof(UserRole.Cashier))); p.RequireClaim(AuthenticationClaimTypes.AccountStatus, AccountStatus.Active.ToString()); });
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (c, ct) => { CreatorPayTelemetry.RateLimitViolations.Add(1); c.HttpContext.Response.ContentType = "application/problem+json"; await c.HttpContext.Response.WriteAsJsonAsync(new { type = "https://httpstatuses.com/429", title = "Too many requests", status = 429, correlationId = c.HttpContext.TraceIdentifier }, ct); };
    o.AddPolicy("auth-sensitive", h => RateLimitPartition.GetFixedWindowLimiter($"{h.Connection.RemoteIpAddress}:{h.Request.Path}", _ => new() { PermitLimit = rateLimits.AuthPermitLimit, Window = TimeSpan.FromSeconds(rateLimits.WindowSeconds), QueueLimit = 0 }));
    o.AddPolicy("financial-sensitive", h => RateLimitPartition.GetFixedWindowLimiter($"{h.User.FindFirst("merchant_id")?.Value ?? h.Connection.RemoteIpAddress?.ToString()}:{h.Request.Path}", _ => new() { PermitLimit = rateLimits.FinancialPermitLimit, Window = TimeSpan.FromSeconds(rateLimits.WindowSeconds), QueueLimit = 0 }));
    o.AddPolicy("admin-report", h => RateLimitPartition.GetFixedWindowLimiter($"{h.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}:{h.Request.Path}", _ => new() { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("public-support", h => RateLimitPartition.GetFixedWindowLimiter($"{h.Connection.RemoteIpAddress}:{h.Request.Path}", _ => new() { PermitLimit = 5, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});

var app = builder.Build();
app.Logger.LogInformation("CreatorPay API starting in {Environment}; version {Version}", app.Environment.EnvironmentName, typeof(Program).Assembly.GetName().Version?.ToString());
app.UseForwardedHeaders(); if (!app.Environment.IsDevelopment()) app.UseHsts();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseExceptionHandler(); app.UseMiddleware<ErrorMonitoringMiddleware>(); app.UseResponseCompression(); app.UseMiddleware<RequestContextMiddleware>();
app.Use(async (context, next) => { context.Response.Headers.XContentTypeOptions = "nosniff"; context.Response.Headers.XFrameOptions = "DENY"; context.Response.Headers["Referrer-Policy"] = "no-referrer"; context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()"; context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'"; context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-site"; context.Response.Headers["Cache-Control"] = "no-store"; await next(); });
app.UseHttpsRedirection(); app.UseCors("Web"); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (app.Environment.IsEnvironment("Pilot") && featureFlags.MaintenanceMode && context.Request.Path.StartsWithSegments("/api/v1/cashier/checkouts"))
    { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; await context.Response.WriteAsJsonAsync(new { title = "Maintenance in progress", status = 503, correlationId = context.TraceIdentifier }); return; }
    if (app.Environment.IsEnvironment("Pilot") && !featureFlags.PublicRegistration && context.Request.Path == "/api/v1/customers/register")
    { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
    if (app.Environment.IsEnvironment("Pilot") && !featureFlags.PublicDiscovery && context.Request.Path.StartsWithSegments("/api/v1/discovery"))
    { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
    await next();
});
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = x => x.Tags.Contains("live"), ResponseWriter = WriteHealth });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = x => x.Tags.Contains("ready"), ResponseWriter = WriteHealth });
app.MapGet("/health", () => Results.Redirect("/health/live")).ExcludeFromDescription();
app.MapAuthEndpoints(); app.MapCreatorEndpoints(); app.MapMerchantEndpoints(); app.MapOrganizationEndpoints(); app.MapPartnershipEndpoints(); app.MapQrEndpoints(); app.MapCommissionEndpoints(); app.MapWalletEndpoints(); app.MapOfflineSyncEndpoints(); app.MapEarningsEndpoints(); app.MapNotificationEndpoints(); app.MapRiskEndpoints();
app.MapAdminEndpoints(); app.MapPilotTestActorEndpoints();
app.MapReportingEndpoints();
app.MapCampaignEndpoints();
app.MapCheckoutEndpoints(); app.MapHub<CheckoutHub>("/hubs/checkout");
app.MapDiscoveryEndpoints();
app.MapSupportEndpoints();
app.MapPilotExperienceEndpoints();
app.MapE2eSeedEndpoints(app.Environment);
app.Run();

static Task WriteHealth(HttpContext context, HealthReport report) { context.Response.ContentType = "application/json"; return context.Response.WriteAsJsonAsync(new { status = report.Status.ToString(), service = "CreatorPay API", correlationId = context.TraceIdentifier }); }
public partial class Program;
