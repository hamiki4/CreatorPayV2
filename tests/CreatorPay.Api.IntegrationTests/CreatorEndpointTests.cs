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

public sealed class CreatorEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Key = "development-only-replace-this-signing-key-000000";
    private readonly WebApplicationFactory<Program> _factory;
    public CreatorEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()));

    [Theory]
    [InlineData("/api/v1/creators/me")]
    [InlineData("/api/v1/creators/pending")]
    [InlineData("/api/v1/creators/approve")]
    public async Task ProtectedCreatorEndpointsRequireAuthentication(string path)
    {
        using var client = _factory.CreateClient(); using var response = path.EndsWith("approve") ? await client.PostAsJsonAsync(path, new { creatorId = Guid.NewGuid() }) : await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatorCannotAccessPlatformApprovalQueue()
    {
        using var client = _factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("Creator"));
        using var response = await client.GetAsync("/api/v1/creators/pending"); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationRejectsInvalidDisplayNameAndWeakPasswordBeforePersistence()
    {
        using var client = _factory.CreateClient(); using var response = await client.PostAsJsonAsync("/api/v1/creators/register", new { firstName = "Ada", lastName = "Lovelace", displayName = "A", phoneNumber = "+12025550123", email = "ada@example.com", password = "weak" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string Token(string role)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role)], expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials));
    }
}
