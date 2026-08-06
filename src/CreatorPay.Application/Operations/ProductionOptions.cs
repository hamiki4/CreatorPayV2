namespace CreatorPay.Application.Operations;

public sealed class WorkerOptions { public const string SectionName = "BackgroundWorkers"; public bool Enabled { get; set; } = true; public int PollIntervalSeconds { get; set; } = 10; public int LockTimeoutMinutes { get; set; } = 15; public string? HealthFilePath { get; set; } }
public sealed class StorageOptions { public const string SectionName = "Storage"; public string Provider { get; set; } = "MetadataOnly"; public string? ConnectionString { get; set; } }
public sealed class ObservabilityOptions { public const string SectionName = "Observability"; public string ServiceName { get; set; } = "CreatorPay"; public string? OtlpEndpoint { get; set; } public bool EnableOtlpExporter { get; set; } }
public sealed class ErrorMonitoringOptions { public const string SectionName = "ErrorMonitoring"; public bool Enabled { get; set; } public string? Endpoint { get; set; } public string? Environment { get; set; } }
public sealed class CorsOptions { public const string SectionName = "Cors"; public string[] AllowedOrigins { get; set; } = []; }
public sealed class HealthOptions { public const string SectionName = "HealthChecks"; public int TimeoutSeconds { get; set; } = 5; public string? OperationsKey { get; set; } }
public sealed class RateLimitOptions { public const string SectionName = "RateLimiting"; public int AuthPermitLimit { get; set; } = 10; public int FinancialPermitLimit { get; set; } = 20; public int WindowSeconds { get; set; } = 60; }
public sealed class ReverseProxyOptions { public const string SectionName = "ReverseProxy"; public string[] KnownProxies { get; set; } = []; }
public sealed class FeatureFlagOptions
{
    public const string SectionName = "FeatureFlags";
    public bool DevelopmentOtpReveal { get; set; }
    public bool DevelopmentInvitationTokenReveal { get; set; }
    public bool RealNotificationProviders { get; set; }
    public bool ExternalSms { get; set; }
    public bool ExternalEmail { get; set; }
    public bool ExternalPaymentProvider { get; set; }
    public bool AutomaticPayouts { get; set; }
    public bool PublicRegistration { get; set; }
    public bool PublicDiscovery { get; set; }
    public bool SupportNotifications { get; set; }
    public bool ProductionMonitoring { get; set; }
    public bool MaintenanceMode { get; set; }
    public bool OfflineSync { get; set; }
    public bool PublicQrResolution { get; set; }
    public bool PlatformAdminOperations { get; set; }
}
public sealed class PilotOptions
{
    public const string SectionName = "Pilot";
    public bool Enabled { get; set; }
    public bool RequireHttps { get; set; } = true;
    public bool AuditLoggingEnabled { get; set; } = true;
    public bool HealthMonitoringEnabled { get; set; } = true;
    public bool ManualWalletFundingOnly { get; set; } = true;
    public int MaximumBusinesses { get; set; } = 10;
    public int MaximumCreators { get; set; } = 25;
    public decimal MaximumPurchaseAmount { get; set; } = 5000m;
    public decimal MaximumCommissionAmount { get; set; } = 500m;
    public decimal DailyMerchantSpendingLimit { get; set; } = 10000m;
    public decimal ShopperCashbackLimit { get; set; } = 500m;
    public decimal CreatorEarningLimit { get; set; } = 1000m;
    public int PayoutHoldDays { get; set; } = 7;
}
