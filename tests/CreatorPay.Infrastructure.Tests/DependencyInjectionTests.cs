using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPay.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructureReturnsServiceCollection()
    {
        ServiceCollection services = [];
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CreatorPayDatabase"] = "Host=localhost;Database=CreatorPayV2Db;Username=test;Password=test"
            })
            .Build();

        Assert.Same(services, services.AddInfrastructure(configuration));
    }
}
