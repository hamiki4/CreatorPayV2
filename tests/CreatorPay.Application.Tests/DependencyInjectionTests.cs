using Microsoft.Extensions.DependencyInjection;

namespace CreatorPay.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationReturnsServiceCollection()
    {
        ServiceCollection services = [];

        Assert.Same(services, services.AddApplication());
    }
}
