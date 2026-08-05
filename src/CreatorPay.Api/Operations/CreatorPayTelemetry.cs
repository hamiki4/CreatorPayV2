using System.Diagnostics;
using System.Diagnostics.Metrics;
namespace CreatorPay.Api.Operations;

public static class CreatorPayTelemetry
{
    public const string Name = "CreatorPay";
    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);
    public static readonly Counter<long> AuthenticationFailures = Meter.CreateCounter<long>("creatorpay.authentication.failures");
    public static readonly Counter<long> RateLimitViolations = Meter.CreateCounter<long>("creatorpay.ratelimit.violations");
    public static readonly Histogram<double> DatabaseDuration = Meter.CreateHistogram<double>("creatorpay.database.duration", "ms");
    public static readonly Counter<long> FailedNotifications = Meter.CreateCounter<long>("creatorpay.notifications.failed");
    public static readonly Histogram<double> CheckoutApprovalLatency = Meter.CreateHistogram<double>("creatorpay.checkout.approval.latency", "ms");
    public static readonly Counter<long> TransactionPostingFailures = Meter.CreateCounter<long>("creatorpay.checkout.posting.failures");
    public static readonly UpDownCounter<long> LowBalanceMerchants = Meter.CreateUpDownCounter<long>("creatorpay.wallet.low_balance");
    public static readonly UpDownCounter<long> PayoutQueue = Meter.CreateUpDownCounter<long>("creatorpay.payout.queue");
    public static readonly Counter<long> TrialCreditExhaustions = Meter.CreateCounter<long>("creatorpay.trial.exhaustions");
}
