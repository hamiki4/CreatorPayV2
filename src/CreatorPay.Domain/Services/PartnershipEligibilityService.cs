using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Services;

public static class PartnershipEligibilityService
{
    public static bool IsEligible(MerchantCreatorPartnership partnership, DateTime suppliedUtc, Guid? locationId = null)
    {
        if (suppliedUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(suppliedUtc));
        if (partnership.Status != PartnershipStatus.Approved || partnership.Creator.Status != CreatorStatus.Active || partnership.Merchant.Status != MerchantStatus.Active) return false;
        if (partnership.StartDateUtc > suppliedUtc || partnership.EndDateUtc <= suppliedUtc) return false;
        var restrictions = partnership.Locations.Where(x => x.IsActive).ToArray();
        if (restrictions.Length == 0) return locationId is null || partnership.Merchant.Locations.Any(x => x.Id == locationId && x.IsActive);
        return locationId.HasValue && restrictions.Any(x => x.MerchantLocationId == locationId && x.MerchantLocation.IsActive);
    }
}
