using CreatorPay.Application.Organization;

namespace CreatorPay.Application.Tests;

public sealed class MerchantScopeAuthorizerTests
{
    private readonly MerchantScopeAuthorizer authorizer=new();
    [Fact] public void SameMerchantIsAllowed(){var merchant=Guid.NewGuid();Assert.True(authorizer.EnsureMerchant(merchant,merchant).Succeeded);}
    [Fact] public void AnotherMerchantIsForbidden(){var result=authorizer.EnsureMerchant(Guid.NewGuid(),Guid.NewGuid());Assert.False(result.Succeeded);Assert.Equal(403,result.StatusCode);}
    [Fact] public void AnotherMerchantsLocationIsForbidden(){var result=authorizer.EnsureLocation(Guid.NewGuid(),Guid.NewGuid());Assert.False(result.Succeeded);Assert.Equal(403,result.StatusCode);}
}
