using CreatorPay.Api.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CreatorPay.Api.IntegrationTests;

public sealed class ProductionConfigurationTests
{
    [Fact]
    public void ProductionRejectsPlaceholderSecretsAndInsecureMonitoringEndpoint()
    {
        var values = new Dictionary<string, string?> { ["ConnectionStrings:CreatorPayDatabase"] = "Host=db;Password=replace-with-secret", ["Authentication:Jwt:Issuer"] = "issuer", ["Authentication:Jwt:Audience"] = "audience", ["Authentication:Jwt:SigningKey"] = "replace-with-at-least-32-random-characters", ["CustomerVerification:HmacSecret"] = new('h', 32), ["CustomerVerification:EncryptionKey"] = new('e', 32), ["Cors:AllowedOrigins:0"] = "https://app.example", ["ErrorMonitoring:Enabled"] = "true", ["ErrorMonitoring:Endpoint"] = "http://monitor.example" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var error = Assert.Throws<InvalidOperationException>(() => ProductionConfiguration.Validate(configuration, new EnvironmentStub()));
        Assert.Contains("placeholder", error.Message); Assert.Contains("HTTPS URI", error.Message);
    }

    private sealed class EnvironmentStub : IHostEnvironment { public string EnvironmentName { get; set; } = Environments.Production; public string ApplicationName { get; set; } = "tests"; public string ContentRootPath { get; set; } = "."; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); }
}
