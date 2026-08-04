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
}
