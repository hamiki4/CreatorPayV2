using CreatorPay.Application.Wallet;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Application.Accounts;

public sealed record EffectiveAccountStatusDto(string EffectiveStatus, string EffectiveStatusReason);

public static class EffectiveAccountStatus
{
    public static EffectiveAccountStatusDto FromCustomer(UserAccount user, DateTime now)
    {
        if (IsDeactivated(user.Status)) return Inactive("Deactivated");
        if (IsLocked(user, now)) return Inactive("Locked");
        if (IsSuspended(user.Status)) return Inactive("Suspended");
        if (IsPending(user.Status)) return Inactive("PendingApproval");
        return Active();
    }

    public static EffectiveAccountStatusDto FromCreator(UserAccount user, Creator creator, DateTime now)
    {
        if (IsDeactivated(user.Status) || IsDeactivated(creator.Status)) return Inactive("Deactivated");
        if (IsLocked(user, now)) return Inactive("Locked");
        if (IsSuspended(user.Status) || IsSuspended(creator.Status)) return Inactive("Suspended");
        if (IsPending(user.Status) || IsPending(creator.Status)) return Inactive("PendingApproval");
        return user.Status == AccountStatus.Active && creator.Status == CreatorStatus.Active ? Active() : Inactive("PendingApproval");
    }

    public static EffectiveAccountStatusDto FromMerchant(UserAccount user, Merchant merchant, WalletDto wallet, DateTime now)
    {
        if (IsDeactivated(user.Status) || IsDeactivated(merchant.Status)) return Inactive("Deactivated");
        if (IsLocked(user, now)) return Inactive("Locked");
        if (IsSuspended(user.Status) || IsSuspended(merchant.Status)) return Inactive("Suspended");
        if (IsPending(user.Status) || IsPending(merchant.Status)) return Inactive("PendingApproval");
        if (wallet.AvailableBalance < wallet.MinimumRequiredBalance) return Inactive("Underfunded");
        return Active();
    }

    public static EffectiveAccountStatusDto FromStaff(UserAccount user, bool isActive, DateTime now)
    {
        if (IsDeactivated(user.Status)) return Inactive("Deactivated");
        if (IsLocked(user, now)) return Inactive("Locked");
        if (IsSuspended(user.Status)) return Inactive("Suspended");
        return user.Status == AccountStatus.Active && isActive ? Active() : Inactive("Inactive");
    }

    public static EffectiveAccountStatusDto FromAccount(UserAccount user, DateTime now)
    {
        if (IsDeactivated(user.Status)) return Inactive("Deactivated");
        if (IsLocked(user, now)) return Inactive("Locked");
        if (IsSuspended(user.Status)) return Inactive("Suspended");
        if (IsPending(user.Status)) return Inactive("PendingApproval");
        return Active();
    }

    private static bool IsLocked(UserAccount user, DateTime now) => user.LockoutEndUtc is not null && user.LockoutEndUtc > now;
    private static bool IsPending(AccountStatus status) => status is AccountStatus.PendingVerification or AccountStatus.PendingApproval or AccountStatus.Draft;
    private static bool IsPending(CreatorStatus status) => status is CreatorStatus.PendingVerification or CreatorStatus.PendingApproval or CreatorStatus.CorrectionRequested;
    private static bool IsPending(MerchantStatus status) => status is MerchantStatus.PendingVerification or MerchantStatus.PendingApproval or MerchantStatus.PendingReview or MerchantStatus.CorrectionRequested;
    private static bool IsSuspended(AccountStatus status) => status == AccountStatus.Suspended;
    private static bool IsDeactivated(AccountStatus status) => status is AccountStatus.Rejected or AccountStatus.Closed;
    private static bool IsSuspended(CreatorStatus status) => status == CreatorStatus.Suspended;
    private static bool IsDeactivated(CreatorStatus status) => status is CreatorStatus.Rejected or CreatorStatus.Closed;
    private static bool IsSuspended(MerchantStatus status) => status == MerchantStatus.Suspended;
    private static bool IsDeactivated(MerchantStatus status) => status is MerchantStatus.Rejected or MerchantStatus.Closed;
    private static EffectiveAccountStatusDto Active() => new("Active", "Active");
    private static EffectiveAccountStatusDto Inactive(string reason) => new("Inactive", reason);
}
