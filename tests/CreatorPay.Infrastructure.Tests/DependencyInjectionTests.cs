using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPay.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructureReturnsServiceCollection()
    {
        ServiceCollection services = [];
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Assert.Same(services, services.AddInfrastructure(configuration));
    }
}
