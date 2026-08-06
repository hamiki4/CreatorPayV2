using CreatorPay.Application.Merchants;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Tests;

public sealed class BusinessTypeOfferReuseTests
{
    [Fact]
    public void Public_business_type_catalog_is_complete()
    {
        Assert.Equal(["Restaurant / Café", "Grocery / Mini-market", "Clothing / Boutique", "Beauty / Salon", "Furniture", "Electronics", "Hotel / Travel", "Professional Services", "Other"], BusinessTypes.Values);
    }

    [Theory]
    [InlineData("Restaurant / Café", OfferReuseRule.OncePerDay)]
    [InlineData("Grocery / Mini-market", OfferReuseRule.OncePerDay)]
    [InlineData("Beauty / Salon", OfferReuseRule.OncePerWeek)]
    [InlineData("Hotel / Travel", OfferReuseRule.OncePerMonth)]
    [InlineData("Furniture", OfferReuseRule.OncePerOffer)]
    public void Business_type_suggests_expected_rule(string businessType, OfferReuseRule expected) => Assert.Equal(expected, BusinessTypes.SuggestedReuseRule(businessType));

    [Fact]
    public void Other_requires_explicit_selection() => Assert.Null(BusinessTypes.SuggestedReuseRule("Other"));

}
