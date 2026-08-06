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

    [Fact]
    public void PilotRejectsUnsafeFlagsAndNonHttpsOrigin()
    {
        var values = ValidPilotValues();
        values["FeatureFlags:AutomaticPayouts"] = "true";
        values["Cors:AllowedOrigins:0"] = "http://pilot.example";
        var error = Assert.Throws<InvalidOperationException>(() => ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new EnvironmentStub { EnvironmentName = "Pilot" }));
        Assert.Contains("AutomaticPayouts", error.Message);
        Assert.Contains("Pilot origins must use HTTPS", error.Message);
    }

    [Fact]
    public void PilotAcceptsSafeExplicitConfiguration()
    {
        ProductionConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(ValidPilotValues()).Build(), new EnvironmentStub { EnvironmentName = "Pilot" });
    }

    private static Dictionary<string, string?> ValidPilotValues() => new()
    {
        ["ConnectionStrings:CreatorPayDatabase"] = "Host=db;Password=" + new string('d', 32),
        ["Authentication:Jwt:Issuer"] = "CreatorPay.Pilot", ["Authentication:Jwt:Audience"] = "CreatorPay.Pilot.Web", ["Authentication:Jwt:SigningKey"] = new('j', 32),
        ["CustomerVerification:HmacSecret"] = new('h', 32), ["CustomerVerification:EncryptionKey"] = new('e', 32),
        ["Cors:AllowedOrigins:0"] = "https://pilot.example", ["Support:Email"] = "pilot@example.invalid", ["Storage:Provider"] = "MetadataOnly",
        ["Pilot:Enabled"] = "true", ["Pilot:RequireHttps"] = "true", ["Pilot:AuditLoggingEnabled"] = "true", ["Pilot:HealthMonitoringEnabled"] = "true", ["Pilot:ManualWalletFundingOnly"] = "true",
        ["Pilot:MaximumBusinesses"] = "10", ["Pilot:MaximumCreators"] = "25", ["Pilot:MaximumPurchaseAmount"] = "5000", ["Pilot:MaximumCommissionAmount"] = "500", ["Pilot:DailyMerchantSpendingLimit"] = "10000", ["Pilot:ShopperCashbackLimit"] = "500", ["Pilot:CreatorEarningLimit"] = "1000", ["Pilot:PayoutHoldDays"] = "7"
    };

    private sealed class EnvironmentStub : IHostEnvironment { public string EnvironmentName { get; set; } = Environments.Production; public string ApplicationName { get; set; } = "tests"; public string ContentRootPath { get; set; } = "."; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); }
}
