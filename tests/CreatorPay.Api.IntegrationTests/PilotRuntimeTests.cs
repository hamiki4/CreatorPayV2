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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CreatorPay.Api.IntegrationTests;

public sealed class PilotRuntimeTests
{
    private const string SigningKey = "pilot-runtime-test-signing-key-000000000000";

    [Fact]
    public async Task Pilot_cors_allows_the_real_web_origin_for_profile_photo_requests()
    {
        await using var factory = Factory("Pilot", new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://pilot.weymela.com",
            ["Cors:AllowedOrigins:1"] = "https://localhost"
        });

        using var client = factory.CreateClient();

        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/creators/me/profile-photo");
        preflight.Headers.TryAddWithoutValidation("Origin", "https://pilot.weymela.com");
        preflight.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        preflight.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,content-type");
        var preflightResponse = await client.SendAsync(preflight);
        Assert.Equal(HttpStatusCode.NoContent, preflightResponse.StatusCode);
        Assert.Equal("https://pilot.weymela.com", preflightResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("POST", preflightResponse.Headers.GetValues("Access-Control-Allow-Methods").Single());

        using var post = new HttpRequestMessage(HttpMethod.Post, "/api/v1/creators/me/profile-photo");
        post.Headers.TryAddWithoutValidation("Origin", "https://pilot.weymela.com");
        post.Content = new MultipartFormDataContent();
        var postResponse = await client.SendAsync(post);
        Assert.True(postResponse.Headers.TryGetValues("Access-Control-Allow-Origin", out var originValues));
        Assert.Equal("https://pilot.weymela.com", originValues.Single());
    }

    [Fact]
    public async Task Pilot_maintenance_and_public_feature_flags_fail_closed()
    {
        await using var factory = Factory("Pilot", new Dictionary<string, string?>
        {
            ["FeatureFlags:MaintenanceMode"] = "true",
            ["FeatureFlags:PublicRegistration"] = "false",
            ["FeatureFlags:PublicDiscovery"] = "false"
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/customers/register", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/discovery/creators")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/api/v1/cashier/checkouts/offer", new { })).StatusCode);
    }

    private static WebApplicationFactory<Program> Factory(string environment, Dictionary<string, string?> overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:CreatorPayDatabase"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=" + new string('d', 32),
            ["Authentication:Jwt:Issuer"] = "CreatorPay",
            ["Authentication:Jwt:Audience"] = "CreatorPay.Web",
            ["Authentication:Jwt:SigningKey"] = SigningKey,
            ["CustomerVerification:HmacSecret"] = new string('h', 32),
            ["CustomerVerification:EncryptionKey"] = new string('e', 32),
            ["SmsOtp:SmsProvider"] = "PilotTest",
            ["SmsOtp:HashSecret"] = new string('o', 32),
            ["SmsOtp:TestCode"] = "654321",
            ["Cors:AllowedOrigins:0"] = "https://pilot.example",
            ["Support:Email"] = "pilot@example.invalid",
            ["Storage:Provider"] = "MetadataOnly"
        };
        foreach (var pair in overrides) values[pair.Key] = pair.Value;
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            foreach (var pair in values) builder.UseSetting(pair.Key, pair.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureLogging(logging => logging.ClearProviders());
        });
    }

    private static string Token()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, UserRole.Cashier.ToString()), new Claim(AuthenticationClaimTypes.AccountStatus, AccountStatus.Active.ToString()), new Claim("merchant_id", Guid.NewGuid().ToString()), new Claim("cashier_id", Guid.NewGuid().ToString()), new Claim("test_token", "true") };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", claims, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials));
    }
}
