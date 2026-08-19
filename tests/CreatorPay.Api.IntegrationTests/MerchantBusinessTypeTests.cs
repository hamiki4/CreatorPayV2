using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace CreatorPay.Api.IntegrationTests;

public sealed class MerchantBusinessTypeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Key = "development-only-replace-this-signing-key-000000";
    private readonly WebApplicationFactory<Program> factory;

    public MerchantBusinessTypeTests(WebApplicationFactory<Program> factory) => this.factory = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.ClearProviders()));

    [Fact]
    public async Task Merchant_registration_accepts_supported_business_type()
    {
        using var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/v1/merchants/register", new
        {
            legalBusinessName = "Demo Shop",
            tradingName = "Demo Shop",
            businessType = "Furniture",
            primaryContactName = "Demo Contact",
            phoneNumber = "+251911234567",
            email = $"merchant-business-type-{suffix}@example.com",
            password = "StrongPassword!123",
            confirmation = "StrongPassword!123",
            businessAddress = "1 Demo Road",
            city = "Addis Ababa",
            region = "Addis Ababa",
            country = "Ethiopia",
            timeZone = "Africa/Addis_Ababa",
            preferredLanguage = "en",
            termsAccepted = true,
            documents = Array.Empty<object>()
        });

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"BadRequest response body: {body}");
        }

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Merchant_registration_rejects_unsupported_business_type()
    {
        using var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        using var response = await client.PostAsJsonAsync("/api/v1/merchants/register", new
        {
            legalBusinessName = "Demo Shop",
            tradingName = "Demo Shop",
            businessType = "LLC",
            primaryContactName = "Demo Contact",
            phoneNumber = $"+25191234{suffix}",
            email = $"merchant-business-type-bad-{suffix}@example.com",
            password = "StrongPassword!123",
            businessAddress = "1 Demo Road",
            city = "Addis Ababa",
            region = "Addis Ababa",
            country = "Ethiopia",
            timeZone = "Africa/Addis_Ababa",
            preferredLanguage = "en",
            termsAccepted = true,
            documents = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Merchant_business_types_endpoint_returns_options()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/merchants/business-types");
        response.EnsureSuccessStatusCode();
        var values = await response.Content.ReadFromJsonAsync<BusinessTypeOption[]>();
        Assert.NotNull(values);
        Assert.Contains(values!, x => x.Value == "Restaurant / Café" && x.AmharicLabel == "ምግብ ቤት / ካፌ");
        Assert.Contains(values!, x => x.Value == "Other" && x.AmharicLabel == "ሌላ");
    }

    private static string Token(string role)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay", "CreatorPay.Web", new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role) }, expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: credentials));
    }

    private sealed class BusinessTypeOption
    {
        public string Value { get; set; } = string.Empty;
        public string EnglishLabel { get; set; } = string.Empty;
        public string AmharicLabel { get; set; } = string.Empty;
    }
}
