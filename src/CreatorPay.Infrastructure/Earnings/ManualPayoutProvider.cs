using CreatorPay.Application.Earnings;
namespace CreatorPay.Infrastructure.Earnings;
public sealed class ManualPayoutProvider:IPayoutProvider
{
 public Task<PayoutProviderResult> SubmitPayoutAsync(Guid id,string? reference,CancellationToken ct)=>Task.FromResult(new PayoutProviderResult("Submitted",Safe(reference),null));
 public Task<PayoutProviderResult> GetPayoutStatusAsync(Guid id,CancellationToken ct)=>Task.FromResult(new PayoutProviderResult("Submitted",null,null));
 public Task<PayoutProviderResult> CancelPayoutAsync(Guid id,CancellationToken ct)=>Task.FromResult(new PayoutProviderResult("Cancelled",null,null));
 private static string? Safe(string? x)=>string.IsNullOrWhiteSpace(x)?null:x.Trim()[..Math.Min(x.Trim().Length,100)];
}
