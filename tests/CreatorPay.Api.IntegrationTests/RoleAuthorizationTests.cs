using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class RoleAuthorizationTests : IAsyncLifetime
{
    private const string Key = "development-only-replace-this-signing-key-000000";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("creatorpay_role_auth").WithUsername("creatorpay").WithPassword("test-only-password").Build();
    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:CreatorPayDatabase"] = database.GetConnectionString(),
            ["Authentication:Jwt:Issuer"] = "CreatorPay",
            ["Authentication:Jwt:Audience"] = "CreatorPay.Web",
            ["Authentication:Jwt:SigningKey"] = Key,
            ["CustomerVerification:HmacSecret"] = new string('h', 32),
            ["CustomerVerification:EncryptionKey"] = new string('e', 32),
            ["SmsOtp:SmsProvider"] = "PilotTest",
            ["SmsOtp:HashSecret"] = new string('o', 32),
            ["SmsOtp:TestCode"] = "654321",
            ["Support:Email"] = "tests@example.invalid",
            ["Storage:Provider"] = "MetadataOnly"
        };
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(x =>
        {
            x.UseEnvironment("Test");
            x.ConfigureLogging(l => l.ClearProviders());
            foreach (var pair in values) x.UseSetting(pair.Key, pair.Value);
            x.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        });
        client = factory.CreateClient();
        await using var db = Db();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (factory is not null) await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    private HttpClient Client => client!;

    [Fact]
    public async Task Protected_endpoint_returns_401_without_a_token()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode);

    [Fact]
    public async Task Push_device_registration_requires_authentication()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/v1/push-devices", new { platform = "web", token = "test-token" })).StatusCode);

    [Fact]
    public async Task Wrong_role_returns_403()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Creator, AccountStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode);
    }

    [Fact]
    public async Task Operations_admin_can_access_operational_review_endpoints()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.OperationsAdmin, AccountStatus.Active));
        var creators = await Client.GetAsync("/api/v1/creators/pending");
        Assert.True(creators.IsSuccessStatusCode, await creators.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/v1/admin/merchants/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/v1/admin/accounts?page=1&pageSize=25&roles=Creator")).StatusCode);
    }

    [Fact]
    public async Task Operations_admin_cannot_access_dashboard_or_reports()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.OperationsAdmin, AccountStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/v1/admin/reports/financial-summary")).StatusCode);
    }

    [Fact]
    public async Task Operations_admin_cannot_create_or_remove_accounts()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.OperationsAdmin, AccountStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/v1/admin/accounts/create", new { role = "Customer", password = "NeverSent1!", confirmation = "NeverSent1!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync($"/api/v1/admin/accounts/{Guid.NewGuid()}/delete", new { reason = "test" })).StatusCode);
    }

    [Fact]
    public async Task Pending_creator_cannot_open_active_creator_operations()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Creator, AccountStatus.PendingApproval));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/v1/creator/earnings")).StatusCode);
    }

    [Fact]
    public async Task Expired_access_token_returns_401()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.PlatformAdmin, AccountStatus.Active, DateTime.UtcNow.AddMinutes(-2)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode);
    }

    [Fact]
    public async Task No_public_Platform_Admin_registration_endpoint_exists()
        => Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsJsonAsync("/api/v1/platform-admins/register", new { email = "admin@example.com", password = "NeverSent1!" })).StatusCode);

    [Fact]
    public async Task Repeat_use_override_endpoints_require_authentication()
    {
        Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync($"/api/v1/merchant/repeat-use-approvals/{Guid.NewGuid()}/approve", new { reason = "Reviewed duplicate" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/v1/cashier/checkouts/repeat-use-approvals", new { checkoutId = Guid.NewGuid(), reason = "Customer requested repeat use" })).StatusCode);
    }

    [Fact]
    public async Task Cashier_cannot_decide_repeat_use_override()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Cashier, AccountStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync($"/api/v1/merchant/repeat-use-approvals/{Guid.NewGuid()}/approve", new { reason = "Self approval attempt" })).StatusCode);
    }

    [Fact]
    public async Task Pilot_feedback_requires_authentication()
    {
        Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/v1/pilot/feedback", new { category = "Usability", message = "The navigation could be clearer.", preferredLanguage = "en", context = "/shopper" })).StatusCode);
    }

    [Fact]
    public async Task Checkout_survey_is_limited_to_shoppers()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Creator, AccountStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/v1/pilot/checkout-survey", new { checkoutId = "CHK-test", rating = 5, preferredLanguage = "en" })).StatusCode);
    }

    private static string Token(UserRole role, AccountStatus status, DateTime? expires = null)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role.ToString()), new Claim(AuthenticationClaimTypes.AccountStatus, status.ToString()), new Claim("test_token", "true") };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: expires ?? DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials));
    }

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options);
}
