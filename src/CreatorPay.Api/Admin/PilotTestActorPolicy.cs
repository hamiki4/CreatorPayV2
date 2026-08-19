using CreatorPay.Domain.Enums;

namespace CreatorPay.Api.Admin;

public static class PilotTestActorPolicy
{
    public static bool IsEnabled(string environmentName, UserRole role) =>
        string.Equals(environmentName, "Pilot", StringComparison.OrdinalIgnoreCase) && role == UserRole.PlatformAdmin;

    public static bool IsDisposableLabel(string value) => value.StartsWith("PILOT-E2E-", StringComparison.Ordinal);
}
