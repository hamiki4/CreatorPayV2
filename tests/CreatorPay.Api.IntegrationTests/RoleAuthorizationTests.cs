using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CreatorPay.Api.IntegrationTests;

public sealed class RoleAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Key = "development-only-replace-this-signing-key-000000";
    private readonly HttpClient client;
    public RoleAuthorizationTests(WebApplicationFactory<Program> factory) => client = factory.WithWebHostBuilder(x => x.ConfigureLogging(l => l.ClearProviders())).CreateClient();

    [Fact]
    public async Task Protected_endpoint_returns_401_without_a_token()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode);

    [Fact]
    public async Task Wrong_role_returns_403()
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Creator, AccountStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode);
    }

    [Fact]
    public async Task Pending_creator_cannot_open_active_creator_operations()
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Creator, AccountStatus.PendingApproval));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/creator/earnings")).StatusCode);
    }

    [Fact]
    public async Task Expired_access_token_returns_401()
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.PlatformAdmin, AccountStatus.Active, DateTime.UtcNow.AddMinutes(-2)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/dashboard/summary")).StatusCode);
    }

    [Fact]
    public async Task No_public_Platform_Admin_registration_endpoint_exists()
        => Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/platform-admins/register", new { email = "admin@example.com", password = "NeverSent1!" })).StatusCode);

    [Fact]
    public async Task Repeat_use_override_endpoints_require_authentication()
    {
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/v1/merchant/repeat-use-approvals/{Guid.NewGuid()}/approve", new { reason = "Reviewed duplicate" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/cashier/checkouts/repeat-use-approvals", new { checkoutId = Guid.NewGuid(), reason = "Customer requested repeat use" })).StatusCode);
    }

    [Fact]
    public async Task Cashier_cannot_decide_repeat_use_override()
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(UserRole.Cashier, AccountStatus.Active));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/merchant/repeat-use-approvals/{Guid.NewGuid()}/approve", new { reason = "Self approval attempt" })).StatusCode);
    }

    private static string Token(UserRole role, AccountStatus status, DateTime? expires = null)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role.ToString()), new Claim(AuthenticationClaimTypes.AccountStatus, status.ToString()), new Claim("test_token", "true") };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: expires ?? DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials));
    }
}
