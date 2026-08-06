namespace CreatorPay.Api.Operations;

public interface IErrorMonitoringHook { void Capture(Exception exception, HttpContext context); }
public sealed class LoggingErrorMonitoringHook(ILogger<LoggingErrorMonitoringHook> logger) : IErrorMonitoringHook
{
    public void Capture(Exception exception, HttpContext context) => logger.LogError(exception, "Unhandled API error at {RequestPath}; correlation {CorrelationId}", context.Request.Path, context.TraceIdentifier);
}
public sealed class ErrorMonitoringMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IErrorMonitoringHook monitoring)
    { try { await next(context); } catch (Exception exception) { monitoring.Capture(exception, context); throw; } }
}
