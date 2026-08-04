using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Domain.Services;
namespace CreatorPay.Domain.Tests;

public sealed class PartnershipEligibilityTests
{
    static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
    static MerchantCreatorPartnership Eligible() { var p = new MerchantCreatorPartnership { RequestedAtUtc = Now, Creator = new() { Status = CreatorStatus.Active }, Merchant = new() { Status = MerchantStatus.Active } }; p.Approve(Now, Guid.NewGuid(), Now.AddHours(-1), Now.AddHours(1)); return p; }
    [Fact] public void EligibleDuringPeriod() => Assert.True(PartnershipEligibilityService.IsEligible(Eligible(), Now));
    [Fact] public void DeniedBeforeStart() { var p = Eligible(); p.SetDates(Now.AddMinutes(1), null, Now, Guid.NewGuid()); Assert.False(PartnershipEligibilityService.IsEligible(p, Now)); }
    [Fact] public void DeniedAfterEnd() { var p = Eligible(); p.SetDates(null, Now, Now, Guid.NewGuid()); Assert.False(PartnershipEligibilityService.IsEligible(p, Now)); }
    [Fact] public void DeniedInactiveCreator() { var p = Eligible(); p.Creator.Status = CreatorStatus.Suspended; Assert.False(PartnershipEligibilityService.IsEligible(p, Now)); }
    [Fact] public void LocationMustMatchAndBeActive() { var p = Eligible(); var l = new MerchantLocation { Id = Guid.NewGuid(), IsActive = true }; p.Locations.Add(new() { MerchantLocationId = l.Id, MerchantLocation = l, IsActive = true }); Assert.True(PartnershipEligibilityService.IsEligible(p, Now, l.Id)); Assert.False(PartnershipEligibilityService.IsEligible(p, Now, Guid.NewGuid())); l.IsActive = false; Assert.False(PartnershipEligibilityService.IsEligible(p, Now, l.Id)); }
    [Fact] public void SuspendedReactivates() { var p = Eligible(); p.Suspend(Now, Guid.NewGuid(), "review"); p.Reactivate(Now, Guid.NewGuid()); Assert.Equal(PartnershipStatus.Approved, p.Status); }
}
