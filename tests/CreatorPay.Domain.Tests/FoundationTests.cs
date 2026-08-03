namespace CreatorPay.Domain.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void DomainAssemblyIsAvailable()
    {
        Assert.Equal("CreatorPay.Domain", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
