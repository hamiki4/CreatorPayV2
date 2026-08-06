using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace CreatorPay.Api.IntegrationTests;

public sealed class AdminAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public AdminAuthorizationTests(WebApplicationFactory<Program> factory) => _client = factory.WithWebHostBuilder(x => x.ConfigureLogging(l => l.ClearProviders())).CreateClient();

    [Theory]
    [InlineData("/api/v1/admin/dashboard/summary")]
    [InlineData("/api/v1/admin/dashboard/pilot-metrics")]
    [InlineData("/api/v1/admin/dashboard/pilot-operations")]
    [InlineData("/api/v1/admin/search?q=public-id")]
    [InlineData("/api/v1/admin/audit")]
    [InlineData("/api/v1/admin/system")]
    public async Task Admin_endpoints_require_authentication(string path)
    {
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
