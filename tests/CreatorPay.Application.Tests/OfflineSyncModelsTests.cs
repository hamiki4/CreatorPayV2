using CreatorPay.Application.OfflineSync;
namespace CreatorPay.Application.Tests;

public sealed class OfflineSyncModelsTests
{
    [Fact] public void DefaultsBoundAgeBatchRetriesAndSchema() { var x = new OfflineSyncOptions(); Assert.Equal(24, x.OfflineOperationMaximumAgeHours); Assert.Equal(25, x.MaximumOfflineBatchSize); Assert.Equal(8, x.MaximumOfflineRetryCount); Assert.Equal(1, x.SupportedSchemaVersion); }
    [Fact] public void ResponsePreservesPerItemCorrelation() { var id = Guid.NewGuid(); var x = new OfflineSyncItemResponse("client", "key", "Confirmed", id, "CP-1", null, null, "Purchase confirmed", DateTime.UtcNow, false, "support-1"); Assert.Equal("client", x.ClientOperationId); Assert.Equal(id, x.TransactionId); Assert.False(x.Retryable); }
}
