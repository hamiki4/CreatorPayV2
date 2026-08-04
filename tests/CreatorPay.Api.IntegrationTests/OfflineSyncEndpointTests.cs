using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
namespace CreatorPay.Api.IntegrationTests;

public sealed class OfflineSyncEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    const string Key = "development-only-replace-this-signing-key-000000"; readonly WebApplicationFactory<Program> factory;
    public OfflineSyncEndpointTests(WebApplicationFactory<Program> value) => factory = value.WithWebHostBuilder(x => x.ConfigureLogging(l => l.ClearProviders()));
    [Fact] public async Task OfflineSyncRequiresAuthentication() { using var c = factory.CreateClient(); using var r = await c.PostAsJsonAsync("/api/v1/cashier/offline-sync", new { batchId = Guid.NewGuid(), appVersion = "test", schemaVersion = 1, operations = Array.Empty<object>() }); Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode); }
    [Fact] public async Task WrongRoleCannotSynchronize() { using var c = factory.CreateClient(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("MerchantAdmin")); using var r = await c.PostAsJsonAsync("/api/v1/cashier/offline-sync", new { batchId = Guid.NewGuid(), appVersion = "test", schemaVersion = 1, operations = Array.Empty<object>() }); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }
    static string Token(string role) { var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256); return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)], expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials)); }
}
