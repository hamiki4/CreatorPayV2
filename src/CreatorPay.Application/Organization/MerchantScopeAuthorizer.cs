namespace CreatorPay.Application.Organization;

public sealed class MerchantScopeAuthorizer : IMerchantScopeAuthorizer
{
    public OrganizationResult EnsureMerchant(Guid? claimMerchantId,Guid resourceMerchantId)=>claimMerchantId==resourceMerchantId?OrganizationResult.Ok():OrganizationResult.Fail("The resource is outside your merchant scope.",403);
    public OrganizationResult EnsureLocation(Guid merchantId,Guid locationMerchantId)=>merchantId==locationMerchantId?OrganizationResult.Ok():OrganizationResult.Fail("The location is outside your merchant scope.",403);
}
