using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Tests;

public sealed class OfferReuseRuleTests
{
    [Fact]
    public void Merchant_can_override_suggestion_and_campaign_keeps_final_rule()
    {
        var campaign = new CreatorMerchantCampaign { Id = Guid.NewGuid(), PublicCampaignId = "CMP-RULE", CreatorId = Guid.NewGuid(), MerchantId = Guid.NewGuid(), MerchantCreatorPartnershipId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow };
        campaign.Approve(30, null, Guid.NewGuid(), Guid.NewGuid(), "RULE01", null, DateTime.UtcNow, OfferReuseRule.Unlimited);
        Assert.Equal(OfferReuseRule.Unlimited, campaign.ReuseRule);
    }

    [Theory]
    [InlineData(OfferReuseRule.OncePerDay)]
    [InlineData(OfferReuseRule.OncePerWeek)]
    [InlineData(OfferReuseRule.OncePerMonth)]
    [InlineData(OfferReuseRule.OncePerOffer)]
    [InlineData(OfferReuseRule.Unlimited)]
    public void Every_supported_rule_is_stored(OfferReuseRule rule)
    {
        var campaign = new CreatorMerchantCampaign { Id = Guid.NewGuid(), PublicCampaignId = "CMP-RULE", CreatorId = Guid.NewGuid(), MerchantId = Guid.NewGuid(), MerchantCreatorPartnershipId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow };
        campaign.Approve(30, null, Guid.NewGuid(), Guid.NewGuid(), "RULE01", null, DateTime.UtcNow, rule);
        Assert.Equal(rule, campaign.ReuseRule);
    }
}
