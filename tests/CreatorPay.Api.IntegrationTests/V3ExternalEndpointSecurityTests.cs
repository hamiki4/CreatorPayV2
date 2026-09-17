using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CreatorPay.Api.IntegrationTests;

public sealed class V3ExternalEndpointSecurityTests : IDisposable
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public V3ExternalEndpointSecurityTests()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:CreatorPayDatabase"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Password=" + new string('d', 32),
            ["Authentication:Jwt:Issuer"] = "CreatorPay",
            ["Authentication:Jwt:Audience"] = "CreatorPay.Web",
            ["Authentication:Jwt:SigningKey"] = "integration-test-signing-key-0000000000000000",
            ["V3Integration:Enabled"] = "true",
            ["V3Integration:Issuer"] = "weymela-v3-test",
            ["V3Integration:Audience"] = "creatorpay-test",
            ["V3Integration:Environment"] = "Test",
            ["V3Integration:V3ApiUrl"] = "https://v3-api.test/",
            ["V3Integration:V3WebUrl"] = "https://v3.test/",
            ["V3Integration:ProductWebUrl"] = "https://localhost/",
            ["V3Integration:CallbackId"] = "integration-callback",
            ["V3Integration:ClientId"] = "integration-test-client",
            ["V3Integration:ClientSecret"] = new string('s', 48)
        };
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            foreach (var pair in values) builder.UseSetting(pair.Key, pair.Value);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(values));
            builder.ConfigureLogging(logging => logging.ClearProviders());
        });
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Begin_sets_a_secure_strict_http_only_state_cookie_and_callback_rejects_state_mismatch()
    {
        using var begin = await client.PostAsync("/api/v1/integration/v3/begin",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["role"] = "Customer",
                ["purpose"] = "PROFILE_ONBOARDING"
            }));
        Assert.Equal(HttpStatusCode.Redirect, begin.StatusCode);
        Assert.StartsWith("https://v3.test/product-handoff?", begin.Headers.Location?.ToString(),
            StringComparison.Ordinal);
        var cookie = Assert.Single(begin.Headers.GetValues("Set-Cookie"));
        Assert.Contains("HttpOnly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", cookie, StringComparison.OrdinalIgnoreCase);

        using var callback = await client.PostAsync("/api/v1/integration/v3/callback",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["state"] = new string('x', 48),
                ["code"] = new string('c', 48),
                ["callbackId"] = "integration-callback"
            }));
        Assert.Equal(HttpStatusCode.Unauthorized, callback.StatusCode);
    }

    [Fact]
    public async Task Same_origin_browser_fetch_keeps_begin_out_of_navigation_history()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integration/v3/begin")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["role"] = "Business",
                ["purpose"] = "PROFILE_ONBOARDING"
            })
        };
        request.Headers.Add("Origin", "https://localhost");
        request.Headers.Add("X-Weymela-Product-Request", "1");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.True(payload.TryGetValue("state", out var state));
        Assert.InRange(state.Length, 32, 160);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("HttpOnly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Integration_navigation_endpoints_do_not_accept_direct_gets()
    {
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await client.GetAsync("/api/v1/integration/v3/begin")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await client.GetAsync("/api/v1/integration/v3/callback")).StatusCode);
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }
}
