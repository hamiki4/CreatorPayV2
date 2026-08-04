using System.Diagnostics;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace CreatorPay.Api.Operations;

public sealed partial class RequestContextMiddleware(RequestDelegate next, ILogger<RequestContextMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)] private static partial Regex ValidCorrelationId();
    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = supplied is not null && ValidCorrelationId().IsMatch(supplied) ? supplied : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId; context.Response.Headers[HeaderName] = correlationId;
        var started = Stopwatch.GetTimestamp();
        using var scope = logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = correlationId, ["RequestId"] = context.TraceIdentifier, ["UserId"] = context.User.FindFirstValue(ClaimTypes.NameIdentifier), ["MerchantId"] = context.User.FindFirstValue("merchant_id"), ["Endpoint"] = context.Request.Path.Value });
        try { await next(context); }
        finally { logger.LogInformation("HTTP {Method} {Endpoint} responded {StatusCode} in {DurationMs:F1} ms", context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }
}
