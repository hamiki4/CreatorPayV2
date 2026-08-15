using CreatorPay.Api.Admin;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Api.IntegrationTests;

public sealed class PilotTestActorPolicyTests
{
    [Fact] public void Only_platform_admin_in_pilot_is_enabled()
    {
        Assert.True(PilotTestActorPolicy.IsEnabled("Pilot", UserRole.PlatformAdmin));
        Assert.False(PilotTestActorPolicy.IsEnabled("Production", UserRole.PlatformAdmin));
        Assert.False(PilotTestActorPolicy.IsEnabled("Pilot", UserRole.Customer));
        Assert.False(PilotTestActorPolicy.IsEnabled("Pilot", UserRole.Creator));
        Assert.False(PilotTestActorPolicy.IsEnabled("Pilot", UserRole.MerchantAdmin));
        Assert.False(PilotTestActorPolicy.IsEnabled("Pilot", UserRole.Cashier));
    }

    [Fact] public void Cleanup_accepts_only_explicit_disposable_labels()
    {
        Assert.True(PilotTestActorPolicy.IsDisposableLabel("PILOT-E2E-20260815-abc"));
        Assert.False(PilotTestActorPolicy.IsDisposableLabel("real-user"));
        Assert.False(PilotTestActorPolicy.IsDisposableLabel("PILOT-20260815"));
    }
}
