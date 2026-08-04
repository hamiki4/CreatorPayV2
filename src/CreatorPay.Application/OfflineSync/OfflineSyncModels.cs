namespace CreatorPay.Application.OfflineSync;

public sealed class OfflineSyncOptions
{
    public const string SectionName = "OfflineSync";
    public int OfflineOperationMaximumAgeHours { get; set; } = 24;
    public int OfflineDraftRetentionDays { get; set; } = 7;
    public int CompletedOperationRetentionDays { get; set; } = 7;
    public int MaximumOfflineBatchSize { get; set; } = 25;
    public int MaximumOfflineRetryCount { get; set; } = 8;
    public int SupportedSchemaVersion { get; set; } = 1;
}
