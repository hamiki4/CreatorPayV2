using CreatorPay.Application.OfflineSync;
namespace CreatorPay.Application.Tests;

public sealed class OfflineSyncModelsTests
{
    [Fact] public void DefaultsBoundAgeBatchRetriesAndSchema() { var x = new OfflineSyncOptions(); Assert.Equal(24, x.OfflineOperationMaximumAgeHours); Assert.Equal(25, x.MaximumOfflineBatchSize); Assert.Equal(8, x.MaximumOfflineRetryCount); Assert.Equal(1, x.SupportedSchemaVersion); }
    [Fact] public void Wallet_contract_has_no_direct_purchase_operation() { Assert.DoesNotContain(typeof(CreatorPay.Application.Wallet.IWalletService).GetMethods(), x => x.Name.Contains("ConfirmPurchase", StringComparison.Ordinal)); }
}
