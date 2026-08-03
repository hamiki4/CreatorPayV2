namespace CreatorPay.Domain.Enums;

public enum CommissionScopeType { PlatformDefault, MerchantDefault, PartnershipOverride, CampaignOverride }
public enum CommissionRuleSourceType { PlatformDefault, MerchantDefault, Partnership, Campaign }
public enum CommissionRoundingMode { AwayFromZero, ToEven, Down, Up }
