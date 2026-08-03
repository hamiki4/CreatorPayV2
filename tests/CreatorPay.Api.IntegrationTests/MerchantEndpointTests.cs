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

public sealed class MerchantEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Key="development-only-replace-this-signing-key-000000";private readonly WebApplicationFactory<Program> factory;public MerchantEndpointTests(WebApplicationFactory<Program> value)=>factory=value.WithWebHostBuilder(b=>b.ConfigureLogging(l=>l.ClearProviders()));
    [Theory][InlineData("/api/v1/merchants/me")][InlineData("/api/v1/admin/merchants/pending")][InlineData("/api/v1/admin/merchants/approve")]
    public async Task ProtectedEndpointsRequireAuthentication(string path){using var client=factory.CreateClient();using var response=path.EndsWith("approve")?await client.PostAsJsonAsync(path,new{merchantId=Guid.NewGuid()}):await client.GetAsync(path);Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);}
    [Theory][InlineData("Creator")][InlineData("MerchantAdmin")]
    public async Task NonPlatformAdminsCannotAccessApprovalQueue(string role){using var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Token(role));using var response=await client.GetAsync("/api/v1/admin/merchants/pending");Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);}
    [Fact]public async Task CreatorCannotAccessMerchantProfile(){using var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Token("Creator"));using var response=await client.GetAsync("/api/v1/merchants/me");Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);}
    [Theory][InlineData("/api/v1/merchant/locations")][InlineData("/api/v1/merchant/supervisors")][InlineData("/api/v1/merchant/cashiers")]
    public async Task OrganizationEndpointsRequireAuthentication(string path){using var client=factory.CreateClient();using var response=await client.GetAsync(path);Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);}
    [Theory][InlineData("Creator")][InlineData("Cashier")][InlineData("Supervisor")]
    public async Task NonMerchantAdminsCannotManageStaff(string role){using var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",Token(role));using var response=await client.GetAsync("/api/v1/merchant/cashiers");Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);}
    [Fact]public async Task RegistrationRejectsMissingAddressAndWeakPasswordBeforePersistence(){using var client=factory.CreateClient();using var response=await client.PostAsJsonAsync("/api/v1/merchants/register",new{legalBusinessName="Acme Ltd",tradingName="Acme",businessType="LLC",phoneNumber="+12025550123",email="merchant@example.com",password="weak",businessAddress="",city="",region="",country="",timeZone="UTC"});Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    [Fact]public async Task RegistrationRejectsInvalidBusinessTypeAndPhone(){using var client=factory.CreateClient();using var response=await client.PostAsJsonAsync("/api/v1/merchants/register",new{legalBusinessName="Acme Ltd",tradingName="Acme",businessType="",phoneNumber="123",email="merchant@example.com",password="StrongPassword!123",businessAddress="1 Main St",city="Boston",region="MA",country="US",timeZone="UTC"});Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);}
    private static string Token(string role){var credentials=new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),SecurityAlgorithms.HmacSha256);return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("CreatorPay","CreatorPay.Web",[new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,role)],expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:credentials));}
}
