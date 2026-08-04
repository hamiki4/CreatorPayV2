using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace CreatorPay.Api.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();
    }

    [Fact]
    public async Task HealthReturnsSuccess()
    {
        using HttpResponseMessage response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("CreatorPay API", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LiveHealthAndCorrelationIdAreAvailable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live"); request.Headers.Add("X-Correlation-ID", "test-correlation-15");
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("test-correlation-15", response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single()); Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("base-uri 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCorrelationIdIsReplaced()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live"); request.Headers.Add("X-Correlation-ID", "invalid value with spaces");
        using var response = await _client.SendAsync(request); Assert.NotEqual("invalid value with spaces", response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task CurrentUserRequiresAuthentication()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
