using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace CreatorPay.Api.IntegrationTests;

public sealed class RegistrationPhoneValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public RegistrationPhoneValidationTests(WebApplicationFactory<Program> value)
        => factory = value.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()));

    [Theory]
    [InlineData("/api/v1/customers/register")]
    [InlineData("/api/v1/creators/register")]
    [InlineData("/api/v1/merchants/register")]
    public async Task Every_public_role_rejects_an_invalid_Ethiopian_phone_with_safe_400(string path)
    {
        using var client = factory.CreateClient();
        object request = path switch
        {
            "/api/v1/customers/register" => new { displayName = "Pilot Shopper", email = "phone-test@example.com", phoneNumber = "0812345678", password = "Welcome1!", confirmation = "Welcome1!" },
            "/api/v1/creators/register" => new { firstName = "Abebe", lastName = "Kebede", displayName = "Abebe", phoneNumber = "0812345678", email = "phone-test@example.com", password = "Welcome1!", preferredLanguage = "en", city = "Addis Ababa", biography = "", contentCategories = "", termsAccepted = true, socialProfiles = new[] { new { platform = "TikTok", handle = "", profileUrl = "https://www.tiktok.com/@abebe", followerCount = 50000, isPrimary = true } } },
            _ => new { legalBusinessName = "Pilot Business", tradingName = "Pilot", businessType = "Grocery / Mini-market", primaryContactName = "Abebe Kebede", phoneNumber = "0812345678", email = "phone-test@example.com", password = "Welcome1!", businessAddress = "Addis Ababa", city = "Addis Ababa", region = "Not provided", country = "Ethiopia", timeZone = "Africa/Addis_Ababa", preferredLanguage = "en", termsAccepted = true, documents = Array.Empty<object>() }
        };

        using var response = await client.PostAsJsonAsync(path, request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Enter a valid Ethiopian mobile number", content);
        Assert.DoesNotContain("Support ID", content, StringComparison.OrdinalIgnoreCase);
    }
}
