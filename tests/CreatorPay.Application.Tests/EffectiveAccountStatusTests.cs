using CreatorPay.Application.Accounts;
using CreatorPay.Application.Wallet;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Tests;

public sealed class EffectiveAccountStatusTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Pending_creator_stays_inactive_until_platform_approval()
    {
        var user = User(UserRole.Creator, AccountStatus.PendingApproval);
        var creator = new Creator { Status = CreatorStatus.PendingApproval };

        var status = EffectiveAccountStatus.FromCreator(user, creator, Now);

        Assert.Equal("Inactive", status.EffectiveStatus);
        Assert.Equal("PendingApproval", status.EffectiveStatusReason);
    }

    [Fact]
    public void Approved_creator_becomes_active()
    {
        var status = EffectiveAccountStatus.FromCreator(User(UserRole.Creator, AccountStatus.Active), new Creator { Status = CreatorStatus.Active }, Now);

        Assert.Equal("Active", status.EffectiveStatus);
        Assert.Equal("Active", status.EffectiveStatusReason);
    }

    [Fact]
    public void Locked_creator_is_inactive_even_after_approval()
    {
        var user = User(UserRole.Creator, AccountStatus.Active);
        user.LockoutEndUtc = Now.AddMinutes(10);

        var status = EffectiveAccountStatus.FromCreator(user, new Creator { Status = CreatorStatus.Active }, Now);

        Assert.Equal("Inactive", status.EffectiveStatus);
        Assert.Equal("Locked", status.EffectiveStatusReason);
    }

    [Fact]
    public void Unlocked_approved_creator_returns_active_again()
    {
        var user = User(UserRole.Creator, AccountStatus.Active);
        user.LockoutEndUtc = Now.AddMinutes(-1);

        var status = EffectiveAccountStatus.FromCreator(user, new Creator { Status = CreatorStatus.Active }, Now);

        Assert.Equal("Active", status.EffectiveStatus);
    }

    [Fact]
    public void Pending_business_is_inactive_before_admin_review()
    {
        var user = User(UserRole.MerchantAdmin, AccountStatus.PendingApproval);
        var merchant = new Merchant { Status = MerchantStatus.PendingReview };
        var wallet = Wallet(0m, 1200m);

        var status = EffectiveAccountStatus.FromMerchant(user, merchant, wallet, Now);

        Assert.Equal("Inactive", status.EffectiveStatus);
        Assert.Equal("PendingApproval", status.EffectiveStatusReason);
    }

    [Fact]
    public void Approved_and_funded_business_is_active()
    {
        var status = EffectiveAccountStatus.FromMerchant(User(UserRole.MerchantAdmin, AccountStatus.Active), new Merchant { Status = MerchantStatus.Active }, Wallet(2000m, 1200m), Now);

        Assert.Equal("Active", status.EffectiveStatus);
    }

    [Fact]
    public void Approved_but_underfunded_business_is_inactive()
    {
        var status = EffectiveAccountStatus.FromMerchant(User(UserRole.MerchantAdmin, AccountStatus.Active), new Merchant { Status = MerchantStatus.Active }, Wallet(1000m, 1200m), Now);

        Assert.Equal("Inactive", status.EffectiveStatus);
        Assert.Equal("Underfunded", status.EffectiveStatusReason);
    }

    [Fact]
    public void Funding_restored_to_threshold_reactivates_business()
    {
        var status = EffectiveAccountStatus.FromMerchant(User(UserRole.MerchantAdmin, AccountStatus.Active), new Merchant { Status = MerchantStatus.Active }, Wallet(1200m, 1200m), Now);

        Assert.Equal("Active", status.EffectiveStatus);
    }

    [Fact]
    public void Locked_business_is_inactive_until_unlock()
    {
        var user = User(UserRole.MerchantAdmin, AccountStatus.Active);
        user.LockoutEndUtc = Now.AddMinutes(5);

        var status = EffectiveAccountStatus.FromMerchant(user, new Merchant { Status = MerchantStatus.Active }, Wallet(5000m, 1200m), Now);

        Assert.Equal("Inactive", status.EffectiveStatus);
        Assert.Equal("Locked", status.EffectiveStatusReason);
    }

    [Fact]
    public void Unlocking_funded_business_restores_active_status()
    {
        var user = User(UserRole.MerchantAdmin, AccountStatus.Active);
        user.LockoutEndUtc = Now.AddMinutes(-1);

        var status = EffectiveAccountStatus.FromMerchant(user, new Merchant { Status = MerchantStatus.Active }, Wallet(5000m, 1200m), Now);

        Assert.Equal("Active", status.EffectiveStatus);
    }

    [Fact]
    public void Business_type_threshold_change_can_make_an_approved_business_inactive_until_restocked()
    {
        var user = User(UserRole.MerchantAdmin, AccountStatus.Active);
        var merchant = new Merchant { Status = MerchantStatus.Active };

        var underThreshold = EffectiveAccountStatus.FromMerchant(user, merchant, Wallet(2000m, 5000m), Now);
        var restored = EffectiveAccountStatus.FromMerchant(user, merchant, Wallet(5000m, 5000m), Now);

        Assert.Equal("Inactive", underThreshold.EffectiveStatus);
        Assert.Equal("Underfunded", underThreshold.EffectiveStatusReason);
        Assert.Equal("Active", restored.EffectiveStatus);
    }

    [Fact]
    public void Customer_lockout_is_reflected_as_inactive()
    {
        var user = User(UserRole.Customer, AccountStatus.Active);
        user.LockoutEndUtc = Now.AddMinutes(15);

        var status = EffectiveAccountStatus.FromCustomer(user, Now);

        Assert.Equal("Inactive", status.EffectiveStatus);
        Assert.Equal("Locked", status.EffectiveStatusReason);
    }

    [Fact]
    public void Cashier_profile_uses_account_activity_not_base_role_only()
    {
        var active = EffectiveAccountStatus.FromStaff(User(UserRole.Cashier, AccountStatus.Active), true, Now);
        var inactive = EffectiveAccountStatus.FromStaff(User(UserRole.Cashier, AccountStatus.Active), false, Now);

        Assert.Equal("Active", active.EffectiveStatus);
        Assert.Equal("Inactive", inactive.EffectiveStatus);
    }

    private static UserAccount User(UserRole role, AccountStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{role}@example.com",
        NormalizedEmail = $"{role}@example.com".ToUpperInvariant(),
        Role = role,
        Status = status
    };

    private static WalletDto Wallet(decimal available, decimal minimum) => new(Guid.NewGuid(), "ETB", available, 0m, "Active", minimum, available >= minimum);
}
