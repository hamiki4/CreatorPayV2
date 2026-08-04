namespace CreatorPay.Application.Operations;

public sealed class WorkerOptions { public const string SectionName = "BackgroundWorkers"; public bool Enabled { get; set; } = true; public int PollIntervalSeconds { get; set; } = 10; public int LockTimeoutMinutes { get; set; } = 15; }
public sealed class StorageOptions { public const string SectionName = "Storage"; public string Provider { get; set; } = "MetadataOnly"; public string? ConnectionString { get; set; } }
public sealed class ObservabilityOptions { public const string SectionName = "Observability"; public string ServiceName { get; set; } = "CreatorPay"; public string? OtlpEndpoint { get; set; } public bool EnableOtlpExporter { get; set; } }
public sealed class CorsOptions { public const string SectionName = "Cors"; public string[] AllowedOrigins { get; set; } = []; }
public sealed class HealthOptions { public const string SectionName = "HealthChecks"; public int TimeoutSeconds { get; set; } = 5; public string? OperationsKey { get; set; } }
public sealed class RateLimitOptions { public const string SectionName = "RateLimiting"; public int AuthPermitLimit { get; set; } = 10; public int FinancialPermitLimit { get; set; } = 20; public int WindowSeconds { get; set; } = 60; }
public sealed class ReverseProxyOptions { public const string SectionName = "ReverseProxy"; public string[] KnownProxies { get; set; } = []; }
public sealed class FeatureFlagOptions { public const string SectionName = "FeatureFlags"; public bool DevelopmentOtpReveal { get; set; } public bool DevelopmentInvitationTokenReveal { get; set; } public bool RealNotificationProviders { get; set; } public bool AutomaticPayouts { get; set; } public bool OfflineSync { get; set; } public bool PublicQrResolution { get; set; } public bool PlatformAdminOperations { get; set; } }
