using System.Reflection;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Tests;

public sealed class DomainBehaviorTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Creator_Approval_ActivatesPendingCreator()
    {
        var approver = Guid.NewGuid();
        var creator = new Creator { Status = CreatorStatus.PendingApproval };
        creator.Approve(Now, approver);
        Assert.Equal(CreatorStatus.Active, creator.Status);
        Assert.Equal(Now, creator.ApprovedAtUtc);
        Assert.Equal(approver, creator.ApprovedByUserId);
    }

    [Fact]
    public void Creator_Suspension_SuspendsActiveCreator()
    {
        var creator = new Creator { Status = CreatorStatus.PendingApproval };
        creator.Approve(Now, Guid.NewGuid());
        creator.Suspend(Now.AddHours(1), "admin");
        Assert.Equal(CreatorStatus.Suspended, creator.Status);
    }

    [Fact]
    public void Creator_Rejection_RequiresPendingApproval()
    {
        var creator = new Creator { Status = CreatorStatus.Draft };
        Assert.Throws<InvalidOperationException>(() => creator.Reject(Now, "admin"));
    }

    [Fact]
    public void Creator_Reactivation_OnlyAllowsSuspendedCreator()
    {
        var creator = new Creator { Status = CreatorStatus.PendingApproval };
        creator.Approve(Now, Guid.NewGuid()); creator.Suspend(Now.AddMinutes(1), "admin"); creator.Reactivate(Now.AddMinutes(2), "admin");
        Assert.Equal(CreatorStatus.Active, creator.Status);
        Assert.Throws<InvalidOperationException>(() => creator.Reactivate(Now.AddMinutes(3), "admin"));
    }

    [Fact]
    public void Merchant_Approval_ActivatesPendingMerchant()
    {
        var merchant = new Merchant { Status = MerchantStatus.PendingApproval };
        merchant.Approve(Now, Guid.NewGuid());
        Assert.Equal(MerchantStatus.Active, merchant.Status);
        Assert.Equal(Now, merchant.ApprovedAtUtc);
    }

    [Fact]
    public void Merchant_Rejection_RequiresPendingApproval()
    {
        var merchant = new Merchant { Status = MerchantStatus.Draft };
        Assert.Throws<InvalidOperationException>(() => merchant.Reject(Now, "admin"));
    }

    [Fact]
    public void Merchant_SuspensionAndReactivation_EnforceTransitions()
    {
        var merchant = new Merchant { Status = MerchantStatus.PendingApproval };
        merchant.Approve(Now, Guid.NewGuid()); merchant.Suspend(Now.AddMinutes(1), "admin"); merchant.Reactivate(Now.AddMinutes(2), "admin");
        Assert.Equal(MerchantStatus.Active, merchant.Status);
        Assert.Throws<InvalidOperationException>(() => merchant.Reactivate(Now.AddMinutes(3), "admin"));
    }

    [Fact]
    public void Partnership_Approval_RecordsApprovalAndPeriod()
    {
        var partnership = PendingPartnership();
        partnership.Approve(Now, Guid.NewGuid(), Now.AddDays(1), Now.AddDays(10));
        Assert.Equal(PartnershipStatus.Approved, partnership.Status);
        Assert.Equal(Now.AddDays(1), partnership.StartDateUtc);
        Assert.Equal(Now.AddDays(10), partnership.EndDateUtc);
    }

    [Fact]
    public void Partnership_Rejection_RequiresAndRecordsReason()
    {
        var partnership = PendingPartnership();
        partnership.Reject(Now, Guid.NewGuid(), "Not aligned");
        Assert.Equal(PartnershipStatus.Rejected, partnership.Status);
        Assert.Equal("Not aligned", partnership.RejectionReason);
    }

    [Fact]
    public void Partnership_Suspension_RequiresApproval()
    {
        var partnership = ApprovedPartnership();
        partnership.Suspend(Now.AddHours(1), Guid.NewGuid(), "Review required");
        Assert.Equal(PartnershipStatus.Suspended, partnership.Status);
        Assert.Equal("Review required", partnership.SuspensionReason);
    }

    [Fact]
    public void Partnership_Revocation_IsFinalForEligibility()
    {
        var partnership = ApprovedPartnership();
        partnership.Revoke(Now.AddHours(1), Guid.NewGuid());
        Assert.Equal(PartnershipStatus.Revoked, partnership.Status);
        Assert.False(partnership.IsTransactionEligibleAt(Now.AddHours(2)));
    }

    [Fact]
    public void Partnership_IsNotEligibleBeforeStartDate()
    {
        var partnership = PendingPartnership();
        partnership.Approve(Now, Guid.NewGuid(), Now.AddDays(1), null);
        Assert.False(partnership.IsTransactionEligibleAt(Now));
    }

    [Fact]
    public void Partnership_IsEligibleDuringActivePeriod()
    {
        var partnership = PendingPartnership();
        partnership.Approve(Now, Guid.NewGuid(), Now, Now.AddDays(2));
        Assert.True(partnership.IsTransactionEligibleAt(Now.AddDays(1)));
    }

    [Fact]
    public void Partnership_IsNotEligibleAtOrAfterEndDate()
    {
        var partnership = PendingPartnership();
        partnership.Approve(Now, Guid.NewGuid(), null, Now.AddDays(1));
        Assert.False(partnership.IsTransactionEligibleAt(Now.AddDays(1)));
    }

    [Theory]
    [InlineData(PartnershipStatus.Pending)]
    [InlineData(PartnershipStatus.Rejected)]
    [InlineData(PartnershipStatus.Suspended)]
    [InlineData(PartnershipStatus.Revoked)]
    [InlineData(PartnershipStatus.Expired)]
    [InlineData(PartnershipStatus.Blocked)]
    public void Partnership_AllNonApprovedStatuses_AreNotEligible(PartnershipStatus status)
    {
        var partnership = PendingPartnership();
        typeof(MerchantCreatorPartnership).GetProperty(nameof(MerchantCreatorPartnership.Status), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(partnership, status);
        Assert.False(partnership.IsTransactionEligibleAt(Now));
    }

    private static MerchantCreatorPartnership PendingPartnership() => new() { RequestedAtUtc = Now };

    private static MerchantCreatorPartnership ApprovedPartnership()
    {
        var partnership = PendingPartnership();
        partnership.Approve(Now, Guid.NewGuid());
        return partnership;
    }
}
