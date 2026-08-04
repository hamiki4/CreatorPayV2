using CreatorPay.Application.Notifications;
using CreatorPay.Application.Operations;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CreatorPay.Application.Campaigns;
using CreatorPay.Application.Checkout;

namespace CreatorPay.Worker;

public sealed class OperationalWorker(IServiceScopeFactory scopes, IOptions<WorkerOptions> configured, ILogger<OperationalWorker> logger) : BackgroundService
{
    private readonly WorkerOptions options = configured.Value;
    private readonly string instanceId = $"{Environment.MachineName}-{Guid.NewGuid():N}";
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CreatorPay worker {InstanceId} starting", instanceId);
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Enabled) await RunJob("NotificationOutbox", async (provider, ct) => await provider.GetRequiredService<INotificationOutboxProcessor>().ProcessBatchAsync(instanceId, ct), stoppingToken);
            if (options.Enabled) await RunJob("CampaignLifecycle", async (provider, ct) => await provider.GetRequiredService<ICampaignService>().ProcessLifecycleAsync(ct), stoppingToken);
            if (options.Enabled) await RunJob("CheckoutExpiration", async (provider, ct) => await provider.GetRequiredService<ICheckoutService>().ExpireAsync(ct), stoppingToken);
            try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, options.PollIntervalSeconds)), stoppingToken); } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
        logger.LogInformation("CreatorPay worker {InstanceId} stopped gracefully", instanceId);
    }
    private async Task RunJob(string jobName, Func<IServiceProvider, CancellationToken, Task<int>> action, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lockKey = StableLockKey(jobName); var acquired = await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_lock({0}) AS \"Value\"", lockKey).SingleAsync(ct);
        if (!acquired) { logger.LogDebug("Skipping overlapping background job {JobName}", jobName); return; }
        var correlationId = Guid.NewGuid().ToString("N"); var execution = new BackgroundJobExecution { Id = Guid.NewGuid(), JobName = jobName, InstanceId = instanceId, StartedAtUtc = DateTime.UtcNow, Status = BackgroundJobStatus.Running, AttemptNumber = 1, CorrelationId = correlationId, CreatedAtUtc = DateTime.UtcNow };
        db.Add(execution); await db.SaveChangesAsync(ct);
        using var logScope = logger.BeginScope(new Dictionary<string, object?> { ["BackgroundJobName"] = jobName, ["CorrelationId"] = correlationId, ["InstanceId"] = instanceId });
        try { execution.ItemsProcessed = await action(scope.ServiceProvider, ct); execution.Status = BackgroundJobStatus.Completed; execution.CompletedAtUtc = DateTime.UtcNow; logger.LogInformation("Background job completed; {ItemsProcessed} items processed", execution.ItemsProcessed); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { execution.Status = BackgroundJobStatus.Cancelled; execution.CompletedAtUtc = DateTime.UtcNow; }
        catch (Exception ex) { execution.Status = BackgroundJobStatus.Failed; execution.FailedAtUtc = DateTime.UtcNow; execution.ErrorCode = ex.GetType().Name; execution.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 1000)]; logger.LogError(ex, "Background job failed"); }
        finally { await db.SaveChangesAsync(CancellationToken.None); await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", lockKey); }
    }
    private static long StableLockKey(string value) { unchecked { long hash = 1469598103934665603; foreach (var c in value) hash = (hash ^ c) * 1099511628211; return hash; } }
}
