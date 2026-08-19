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

    [Fact]
    public async Task Pilot_maximum_purchase_is_enforced_before_financial_posting()
    {
        await using var factory = Factory("Test", new Dictionary<string, string?>
        {
            ["Pilot:Enabled"] = "true",
            ["Pilot:MaximumPurchaseAmount"] = "50"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cashier/checkouts/offer") { Content = JsonContent.Create(new { qrPayload = "creatorpay:offer:test:test", merchantLocationId = Guid.NewGuid(), shopperPhoneNumber = "0911000001", purchaseAmount = 51m }) };
        request.Headers.Add("Idempotency-Key", "pilot-limit-test");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, $"{response.StatusCode}: {body}");
        Assert.True(body.Contains("Pilot maximum purchase amount exceeded", StringComparison.Ordinal), body);
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
