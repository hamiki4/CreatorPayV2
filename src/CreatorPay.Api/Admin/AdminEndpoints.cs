using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Api.Admin;

public static class AdminEndpoints
{
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin").WithTags("Platform administration").RequireAuthorization("PlatformAdminOnly");
        admin.MapGet("/dashboard/summary", Dashboard).RequireRateLimiting("admin-report");
        admin.MapGet("/dashboard/trends", Trends).RequireRateLimiting("admin-report");
        admin.MapGet("/dashboard/pilot-metrics", PilotMetrics).RequireRateLimiting("admin-report");
        admin.MapGet("/dashboard/pilot-operations", PilotOperations).RequireRateLimiting("admin-report");
        admin.MapGet("/creators", Creators);
        admin.MapGet("/merchants", Merchants);
        admin.MapGet("/accounts", Accounts);
        admin.MapGet("/purchases", Purchases);
        admin.MapGet("/partnerships", Partnerships);
        admin.MapGet("/offline-sync", OfflineSync);
        admin.MapGet("/audit", Audit);
        admin.MapGet("/search", Search).RequireRateLimiting("admin-report");
        admin.MapGet("/support", Support).RequireRateLimiting("admin-report");
        admin.MapGet("/system", SystemStatus);
        admin.MapGet("/operational-alerts", Alerts);
        admin.MapPost("/accounts/{id:guid}/unlock", UnlockAccount);
        admin.MapPost("/accounts/{id:guid}/revoke-sessions", RevokeSessions);
        admin.MapPost("/operational-alerts/{id:guid}/acknowledge", (Guid id, AdminReason request, HttpContext h, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => ChangeAlert(id, "Acknowledged", request.Reason, h, u, db, ct));
        admin.MapPost("/operational-alerts/{id:guid}/resolve", (Guid id, AdminReason request, HttpContext h, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => ChangeAlert(id, "Resolved", request.Reason, h, u, db, ct));
        return endpoints;
    }

    private static async Task<IResult> Dashboard(DateTime? from, DateTime? to, Guid? merchantId, ApplicationDbContext db, IWebHostEnvironment env, CancellationToken ct)
    {
        var end = Normalize(to, DateTime.UtcNow); var start = Normalize(from, end.Date); if (start > end) return Results.BadRequest(new { error = "from must not be after to" });
        var purchases = db.PurchaseTransactions.AsNoTracking().Where(x => x.TransactionDateUtc >= start && x.TransactionDateUtc <= end && (!merchantId.HasValue || x.MerchantId == merchantId));
        var commission = from p in purchases join s in db.CommissionCalculationSnapshots.AsNoTracking() on p.CommissionCalculationSnapshotId equals s.Id select s;
        var result = new
        {
            range = new { from = start, to = end },
            pendingCreatorApprovals = await db.Creators.CountAsync(x => x.Status == CreatorStatus.PendingApproval, ct),
            pendingMerchantApprovals = await db.Merchants.CountAsync(x => x.Status == MerchantStatus.PendingReview && (!merchantId.HasValue || x.Id == merchantId), ct),
            pendingDeposits = await db.MerchantDeposits.CountAsync(x => x.Status == MerchantDepositStatus.PendingVerification && (!merchantId.HasValue || x.MerchantId == merchantId), ct),
            openFraudAlerts = await db.FraudAlerts.CountAsync(x => x.Status == FraudAlertStatus.Open && (!merchantId.HasValue || x.MerchantId == merchantId), ct),
            openDisputes = await db.Disputes.CountAsync(x => x.Status == DisputeStatus.Open && (!merchantId.HasValue || x.MerchantId == merchantId), ct),
            failedPayouts = await db.CreatorPayouts.CountAsync(x => x.Status == CreatorPayoutStatus.Failed, ct),
            pendingPayouts = await db.CreatorPayouts.CountAsync(x => x.Status == CreatorPayoutStatus.Scheduled || x.Status == CreatorPayoutStatus.Processing || x.Status == CreatorPayoutStatus.Submitted, ct),
            failedCheckouts = await db.CheckoutSessions.CountAsync(x => x.Status == CheckoutSessionStatus.Rejected || x.Status == CheckoutSessionStatus.Expired || x.Status == CheckoutSessionStatus.Cancelled, ct),
            failedNotifications = await db.NotificationOutboxMessages.CountAsync(x => x.Status == NotificationOutboxStatus.Failed || x.Status == NotificationOutboxStatus.DeadLettered, ct),
            suspiciousActivity = await db.FraudAlerts.CountAsync(x => x.Status == FraudAlertStatus.Open, ct),
            openSupportRequests = await db.SupportRequests.CountAsync(x => x.Status == "Open", ct),
            notificationDeadLetters = await db.NotificationDeadLetters.CountAsync(x => x.Status != NotificationDeadLetterStatus.Resolved, ct),
            walletsBelowThreshold = await db.MerchantWallets.CountAsync(x => x.Status == MerchantWalletStatus.LowBalance && (!merchantId.HasValue || x.MerchantId == merchantId), ct),
            rejectedOfflineSyncItems = await db.OfflineSyncItemResults.CountAsync(x => x.ResultStatus == "Rejected", ct),
            repeatUseAwaitingAction = await db.RepeatUseApprovalRequests.CountAsync(x => x.Status == RepeatUseApprovalStatus.Pending || x.Status == RepeatUseApprovalStatus.AwaitingSupervisorApproval, ct),
            purchases = await purchases.CountAsync(ct),
            commissionGenerated = await commission.SumAsync(x => (decimal?)x.PlatformCommissionAmount, ct) ?? 0,
            creatorEarningsPending = await db.CreatorEarnings.Where(x => x.Status == CreatorEarningStatus.Pending).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            creatorPayoutsScheduled = await db.CreatorPayouts.CountAsync(x => x.Status == CreatorPayoutStatus.Scheduled, ct),
            systemHealth = "Available",
            serviceHealth = "API and database available",
            deploymentStatus = $"{env.EnvironmentName} {typeof(Program).Assembly.GetName().Version}",
            workerStatus = await db.BackgroundJobExecutions.AnyAsync(x => x.Status == BackgroundJobStatus.Running || x.CreatedAtUtc >= DateTime.UtcNow.AddMinutes(-15), ct) ? "Active" : "Unknown",
            outboxBacklog = await db.NotificationOutboxMessages.CountAsync(x => x.Status == NotificationOutboxStatus.Pending || x.Status == NotificationOutboxStatus.Failed, ct)
        };
        return Results.Ok(result);
    }

    private static async Task<IResult> Trends(DateTime? from, DateTime? to, ApplicationDbContext db, CancellationToken ct)
    {
        var end = Normalize(to, DateTime.UtcNow); var start = Normalize(from, end.AddDays(-7));
        var rows = await db.PurchaseTransactions.AsNoTracking().Where(x => x.TransactionDateUtc >= start && x.TransactionDateUtc <= end)
            .GroupBy(x => x.TransactionDateUtc.Date).Select(g => new { date = g.Key, purchases = g.Count(), volume = g.Sum(x => x.PurchaseAmount) }).OrderBy(x => x.date).ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> PilotMetrics(DateTime? from, DateTime? to, ApplicationDbContext db, CancellationToken ct)
    {
        var end = Normalize(to, DateTime.UtcNow); var start = Normalize(from, end.AddDays(-7));
        if (start > end) return Results.BadRequest(new { error = "from must not be after to" });
        var sessions = db.CheckoutSessions.AsNoTracking().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc <= end);
        var completed = await sessions.Where(x => x.Status == CheckoutSessionStatus.Completed).Select(x => new { x.CreatedAtUtc, x.CompletedAtUtc }).ToListAsync(ct);
        var approvalTimes = completed.Where(x => x.CompletedAtUtc.HasValue).Select(x => (x.CompletedAtUtc!.Value - x.CreatedAtUtc).TotalSeconds).OrderBy(x => x).ToArray();
        var journals = await db.FinancialJournals.AsNoTracking().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc <= end)
            .Select(x => new { x.Id, Lines = x.Lines.Select(l => new { l.Type, l.Amount }).ToList() }).ToListAsync(ct);
        var created = await sessions.CountAsync(ct); var rejected = await sessions.CountAsync(x => x.Status == CheckoutSessionStatus.Rejected, ct);
        var expired = await sessions.CountAsync(x => x.Status == CheckoutSessionStatus.Expired, ct); var completedCount = completed.Count;
        return Results.Ok(new
        {
            range = new { from = start, to = end },
            checkoutSessionsCreated = created,
            checkoutSessionsCompleted = completedCount,
            customerApprovalRate = created == 0 ? 0 : Math.Round(completedCount * 100m / created, 2),
            customerRejectionRate = created == 0 ? 0 : Math.Round(rejected * 100m / created, 2),
            qrExpirationRate = created == 0 ? 0 : Math.Round(expired * 100m / created, 2),
            medianApprovalSeconds = approvalTimes.Length == 0 ? (double?)null : approvalTimes[approvalTimes.Length / 2],
            postingFailureCount = await db.OperationalAuditEvents.CountAsync(x => x.CreatedAtUtc >= start && x.CreatedAtUtc <= end && x.EventType == "CheckoutPostingFailed", ct),
            duplicateAttemptCount = await db.OperationalAuditEvents.CountAsync(x => x.CreatedAtUtc >= start && x.CreatedAtUtc <= end && x.EventType.Contains("Duplicate"), ct),
            trialTransactionsRemaining = await db.MerchantTrialCredits.SumAsync(x => (int?)(x.MaximumTransactions - x.ConfirmedTransactionCount), ct) ?? 0,
            merchantsInFundingRequired = await db.Merchants.CountAsync(x => x.Status == MerchantStatus.FundingRestricted, ct),
            lowBalanceMerchants = await db.Merchants.CountAsync(x => x.Status == MerchantStatus.LowBalance, ct),
            creatorPayoutQueueAmount = await db.CreatorPayouts.Where(x => x.Status == CreatorPayoutStatus.Scheduled || x.Status == CreatorPayoutStatus.Processing).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            customerPayoutQueueAmount = await db.CustomerPayoutRequests.Where(x => x.Status == CustomerPayoutStatus.Requested || x.Status == CustomerPayoutStatus.Processing).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            notificationDeliveryFailures = await db.NotificationDeliveryAttempts.CountAsync(x => x.CreatedAtUtc >= start && x.CreatedAtUtc <= end && x.Status == DeliveryAttemptStatus.Failed, ct),
            reversalCount = await db.TransactionReversals.CountAsync(x => x.CreatedAtUtc >= start && x.CreatedAtUtc <= end, ct),
            ledgerImbalanceCount = journals.Count(x => x.Lines.Sum(l => l.Type == JournalLineType.Debit ? l.Amount : -l.Amount) != 0)
        });
    }

    private static async Task<IResult> PilotOperations(DateTime? from, DateTime? to, Guid? merchantId, Guid? creatorId, Guid? locationId, Guid? offerId, TransactionStatus? status, ApplicationDbContext db, CancellationToken ct)
    {
        var end = Normalize(to, DateTime.UtcNow); var start = Normalize(from, end.Date);
        if (start > end) return Results.BadRequest(new { error = "from must not be after to" });
        var purchases = db.PurchaseTransactions.AsNoTracking().Where(x => x.TransactionDateUtc >= start && x.TransactionDateUtc <= end
            && (!merchantId.HasValue || x.MerchantId == merchantId) && (!creatorId.HasValue || x.CreatorId == creatorId)
            && (!locationId.HasValue || x.MerchantLocationId == locationId) && (!offerId.HasValue || x.CampaignId == offerId)
            && (!status.HasValue || x.Status == status));
        var sessions = db.CheckoutSessions.AsNoTracking().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc <= end
            && (!merchantId.HasValue || x.MerchantId == merchantId) && (!creatorId.HasValue || x.CreatorId == creatorId)
            && (!locationId.HasValue || x.MerchantLocationId == locationId) && (!offerId.HasValue || x.CampaignId == offerId));
        return Results.Ok(new
        {
            range = new { from = start, to = end }, filters = new { merchantId, creatorId, locationId, offerId, status },
            pilotBusinesses = await db.Merchants.CountAsync(x => x.Status == MerchantStatus.Active, ct),
            pilotCreators = await db.Creators.CountAsync(x => x.Status == CreatorStatus.Active, ct),
            pilotCashiers = await db.Cashiers.CountAsync(x => x.IsActive && (!merchantId.HasValue || x.MerchantId == merchantId), ct),
            pilotShoppers = await db.Customers.CountAsync(x => x.Status == CustomerStatus.Active, ct),
            activePilotOffers = await db.CreatorMerchantCampaigns.CountAsync(x => x.Status == CampaignStatus.Active && (!merchantId.HasValue || x.MerchantId == merchantId) && (!creatorId.HasValue || x.CreatorId == creatorId), ct),
            todaysCheckouts = await sessions.CountAsync(ct), successfulCheckouts = await sessions.CountAsync(x => x.Status == CheckoutSessionStatus.Completed, ct),
            failedCheckouts = await sessions.CountAsync(x => x.Status == CheckoutSessionStatus.Rejected || x.Status == CheckoutSessionStatus.Expired || x.Status == CheckoutSessionStatus.Cancelled, ct),
            walletBalances = await db.MerchantWallets.Where(x => !merchantId.HasValue || x.MerchantId == merchantId).SumAsync(x => (decimal?)x.AvailableBalance, ct) ?? 0,
            lowBalanceBusinesses = await db.MerchantWallets.CountAsync(x => x.Status == MerchantWalletStatus.LowBalance && (!merchantId.HasValue || x.MerchantId == merchantId), ct),
            pendingCreatorEarnings = await db.CreatorEarnings.Where(x => x.Status == CreatorEarningStatus.Pending && (!creatorId.HasValue || x.CreatorId == creatorId)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            pendingShopperCashback = await db.CustomerPayoutRequests.Where(x => x.Status == CustomerPayoutStatus.Requested || x.Status == CustomerPayoutStatus.Processing).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            pendingPayouts = await db.CreatorPayouts.CountAsync(x => x.Status == CreatorPayoutStatus.Scheduled || x.Status == CreatorPayoutStatus.Processing || x.Status == CreatorPayoutStatus.Submitted, ct),
            failedNotifications = await db.NotificationOutboxMessages.CountAsync(x => x.Status == NotificationOutboxStatus.Failed || x.Status == NotificationOutboxStatus.DeadLettered, ct),
            supportRequests = await db.SupportRequests.CountAsync(x => x.Status == "Open", ct), disputes = await db.Disputes.CountAsync(x => x.Status == DisputeStatus.Open, ct),
            suspiciousActivity = await db.FraudAlerts.CountAsync(x => x.Status == FraudAlertStatus.Open, ct), serviceHealth = "API and database available",
            onboardingProgress = new { businessesApproved = await db.Merchants.CountAsync(x => x.Status == MerchantStatus.Active, ct), creatorsApproved = await db.Creators.CountAsync(x => x.Status == CreatorStatus.Active, ct), activePartnerships = await db.MerchantCreatorPartnerships.CountAsync(x => x.Status == PartnershipStatus.Approved, ct) },
            purchaseCount = await purchases.CountAsync(ct)
        });
    }

    private static async Task<IResult> Creators(string? q, CreatorStatus? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var query = db.Creators.AsNoTracking(); if (status.HasValue) query = query.Where(x => x.Status == status); if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(x => x.PublicCreatorId.Contains(q) || x.DisplayName.Contains(q) || x.Email.Contains(q)); } return Results.Ok(await Page(query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.PublicCreatorId, x.DisplayName, email = MaskEmail(x.Email), phone = MaskPhone(x.PhoneNumber), status = x.Status.ToString(), x.CreatedAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> Merchants(string? q, MerchantStatus? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var query = db.Merchants.AsNoTracking(); if (status.HasValue) query = query.Where(x => x.Status == status); if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(x => x.PublicMerchantId.Contains(q) || x.TradingName.Contains(q) || x.Email.Contains(q)); } return Results.Ok(await Page(query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.PublicMerchantId, x.TradingName, email = MaskEmail(x.Email), phone = MaskPhone(x.PhoneNumber), status = x.Status.ToString(), x.CreatedAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> Accounts(string? q, AccountStatus? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var query = db.UserAccounts.AsNoTracking(); if (status.HasValue) query = query.Where(x => x.Status == status); if (!string.IsNullOrWhiteSpace(q)) { var n = q.Trim().ToUpperInvariant(); query = query.Where(x => x.NormalizedEmail.Contains(n)); } return Results.Ok(await Page(query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, email = MaskEmail(x.Email), role = x.Role.ToString(), status = x.Status.ToString(), x.IsEmailVerified, x.IsPhoneVerified, isLocked = x.LockoutEndUtc > DateTime.UtcNow, x.LastLoginAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> Purchases(string? q, Guid? merchantId, Guid? creatorId, TransactionStatus? status, DateTime? from, DateTime? to, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var query = db.PurchaseTransactions.AsNoTracking(); if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.PublicTransactionId.Contains(q)); if (merchantId.HasValue) query = query.Where(x => x.MerchantId == merchantId); if (creatorId.HasValue) query = query.Where(x => x.CreatorId == creatorId); if (status.HasValue) query = query.Where(x => x.Status == status); if (from.HasValue) query = query.Where(x => x.TransactionDateUtc >= Normalize(from, null)); if (to.HasValue) query = query.Where(x => x.TransactionDateUtc <= Normalize(to, null)); return Results.Ok(await Page(query.OrderByDescending(x => x.TransactionDateUtc).Select(x => new { x.Id, x.PublicTransactionId, x.MerchantId, x.CreatorId, x.MerchantLocationId, x.CashierId, x.PurchaseAmount, x.CurrencyCode, status = x.Status.ToString(), x.TransactionDateUtc, x.CorrelationId, idempotencyKey = MaskKey(x.IdempotencyKey) }), page, pageSize, ct)); }
    private static async Task<IResult> Partnerships(Guid? merchantId, Guid? creatorId, PartnershipStatus? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var q = db.MerchantCreatorPartnerships.AsNoTracking(); if (merchantId.HasValue) q = q.Where(x => x.MerchantId == merchantId); if (creatorId.HasValue) q = q.Where(x => x.CreatorId == creatorId); if (status.HasValue) q = q.Where(x => x.Status == status); return Results.Ok(await Page(q.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.MerchantId, x.CreatorId, status = x.Status.ToString(), x.CreatedAtUtc, x.UpdatedAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> OfflineSync(string? q, Guid? merchantId, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var query = db.OfflineSyncBatches.AsNoTracking(); if (merchantId.HasValue) query = query.Where(x => x.MerchantId == merchantId); if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.PublicBatchId.Contains(q) || x.CorrelationId.Contains(q) || x.Items.Any(i => i.ClientOperationId.Contains(q) || i.IdempotencyKey.Contains(q))); return Results.Ok(await Page(query.OrderByDescending(x => x.ReceivedAtUtc).Select(x => new { x.Id, x.PublicBatchId, x.MerchantId, x.CashierUserId, x.ReceivedAtUtc, x.ItemCount, x.ConfirmedCount, x.ApprovalRequiredCount, x.RejectedCount, x.FailedCount, x.AppVersion, x.CorrelationId }), page, pageSize, ct)); }
    private static async Task<IResult> Audit(DateTime? from, DateTime? to, Guid? userId, string? eventType, string? correlationId, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var q = db.OperationalAuditEvents.AsNoTracking(); if (from.HasValue) q = q.Where(x => x.CreatedAtUtc >= Normalize(from, null)); if (to.HasValue) q = q.Where(x => x.CreatedAtUtc <= Normalize(to, null)); if (userId.HasValue) q = q.Where(x => x.ActorUserId == userId); if (!string.IsNullOrWhiteSpace(eventType)) q = q.Where(x => x.EventType.Contains(eventType)); if (!string.IsNullOrWhiteSpace(correlationId)) q = q.Where(x => x.CorrelationId == correlationId); return Results.Ok(await Page(q.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.EventType, x.ActorUserId, x.MerchantId, x.SubjectId, metadata = RedactMetadata(x.MetadataJson), x.CorrelationId, x.CreatedAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> Search(string q, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3) return Results.BadRequest(new { error = "Enter at least three characters." }); q = q.Trim(); var take = Math.Clamp(pageSize, 1, 25); var results = new List<object>(); results.AddRange(await db.Creators.AsNoTracking().Where(x => x.PublicCreatorId.Contains(q)).Take(take).Select(x => (object)new { category = "Creator", id = x.Id, label = x.PublicCreatorId, detail = x.DisplayName, url = $"/admin/creators?id={x.Id}" }).ToListAsync(ct)); results.AddRange(await db.Merchants.AsNoTracking().Where(x => x.PublicMerchantId.Contains(q)).Take(take).Select(x => (object)new { category = "Merchant", id = x.Id, label = x.PublicMerchantId, detail = x.TradingName, url = $"/admin/merchants?id={x.Id}" }).ToListAsync(ct)); results.AddRange(await db.PurchaseTransactions.AsNoTracking().Where(x => x.PublicTransactionId.Contains(q) || x.CorrelationId == q).Take(take).Select(x => (object)new { category = "Purchase", id = x.Id, label = x.PublicTransactionId, detail = x.Status.ToString(), url = $"/admin/purchases?id={x.Id}" }).ToListAsync(ct)); results.AddRange(await db.CreatorPayouts.AsNoTracking().Where(x => x.PublicPayoutId.Contains(q) || x.CorrelationId == q).Take(take).Select(x => (object)new { category = "Payout", id = x.Id, label = x.PublicPayoutId, detail = x.Status.ToString(), url = $"/admin/payouts?id={x.Id}" }).ToListAsync(ct)); return Results.Ok(new { items = results.Skip(Math.Max(0, page - 1) * take).Take(take), page, pageSize = take, total = results.Count }); }
    private static async Task<IResult> SystemStatus(ApplicationDbContext db, IWebHostEnvironment env, CancellationToken ct) => Results.Ok(new { health = "Available", database = "Connected", worker = await db.BackgroundJobExecutions.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.JobName, status = x.Status.ToString(), x.CreatedAtUtc }).FirstOrDefaultAsync(ct), outboxBacklog = await db.NotificationOutboxMessages.CountAsync(x => x.Status == NotificationOutboxStatus.Pending || x.Status == NotificationOutboxStatus.Failed, ct), failedJobs = await db.BackgroundJobExecutions.CountAsync(x => x.Status == BackgroundJobStatus.Failed, ct), migrationStatus = "Use EF migration validation", version = typeof(Program).Assembly.GetName().Version?.ToString(), environment = env.EnvironmentName, providerReadiness = "Configured providers only", otlpReadiness = "Configuration dependent" });
    private static async Task<IResult> Support(string? q, string? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct) { var query = db.SupportRequests.AsNoTracking(); if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.PublicReference.Contains(q)); if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status); return Results.Ok(await Page(query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.PublicReference, x.UserType, x.Subject, x.PreferredLanguage, x.Status, x.CreatedAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> Alerts(string? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct) { var q = db.OperationalAlerts.AsNoTracking(); if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status); return Results.Ok(await Page(q.OrderByDescending(x => x.DetectedAtUtc).Select(x => new { x.Id, x.AlertType, x.Severity, x.Status, x.Title, x.SafeDescription, x.RelatedEntityType, x.RelatedEntityId, x.DetectedAtUtc, x.AcknowledgedAtUtc, x.ResolvedAtUtc, x.CorrelationId }), page, pageSize, ct)); }

    private static async Task<IResult> UnlockAccount(Guid id, AdminReason request, HttpContext h, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct) { var account = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id, ct); if (account is null) return Results.NotFound(); account.FailedLoginCount = 0; account.LockoutEndUtc = null; await AddAudit(db, user.UserAccountId!.Value, "AdminAccountUnlocked", id, request.Reason, h.TraceIdentifier, ct); return Results.NoContent(); }
    private static async Task<IResult> RevokeSessions(Guid id, AdminReason request, HttpContext h, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct) { if (!await db.UserAccounts.AnyAsync(x => x.Id == id, ct)) return Results.NotFound(); var tokens = await db.RefreshTokens.Where(x => x.UserAccountId == id && x.RevokedAtUtc == null).ToListAsync(ct); foreach (var token in tokens) token.RevokedAtUtc = DateTime.UtcNow; await AddAudit(db, user.UserAccountId!.Value, "AdminSessionsRevoked", id, request.Reason, h.TraceIdentifier, ct); return Results.NoContent(); }
    private static async Task<IResult> ChangeAlert(Guid id, string status, string reason, HttpContext h, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct) { var alert = await db.OperationalAlerts.SingleOrDefaultAsync(x => x.Id == id, ct); if (alert is null) return Results.NotFound(); var old = alert.Status; alert.Status = status; alert.UpdatedAtUtc = DateTime.UtcNow; if (status == "Acknowledged") { alert.AcknowledgedAtUtc = DateTime.UtcNow; alert.AcknowledgedByUserId = user.UserAccountId; } if (status == "Resolved") alert.ResolvedAtUtc = DateTime.UtcNow; db.OperationalAlertHistories.Add(new() { Id = Guid.NewGuid(), OperationalAlertId = id, PreviousStatus = old, NewStatus = status, ActorUserId = user.UserAccountId!.Value, Reason = reason, ChangedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow }); await AddAudit(db, user.UserAccountId.Value, $"OperationalAlert{status}", id, reason, h.TraceIdentifier, ct); return Results.NoContent(); }
    private static async Task AddAudit(ApplicationDbContext db, Guid actor, string eventType, Guid subject, string reason, string correlation, CancellationToken ct) { db.OperationalAuditEvents.Add(new() { Id = Guid.NewGuid(), EventType = eventType, ActorUserId = actor, SubjectId = subject, MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { reason }), CorrelationId = correlation, CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); }
    private static async Task<object> Page<T>(IQueryable<T> query, int page, int pageSize, CancellationToken ct) { page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, MaxPageSize); var total = await query.CountAsync(ct); var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); return new { items, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) }; }
    private static DateTime Normalize(DateTime? value, DateTime? fallback) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : fallback!.Value;
    private static string MaskPhone(string value) => value.Length <= 4 ? "****" : $"***-***-{value[^4..]}";
    private static string MaskEmail(string value) { var at = value.IndexOf('@'); return at <= 1 ? "***" : $"{value[0]}***{value[(at - 1)..]}"; }
    private static string MaskKey(string value) => value.Length < 9 ? "[redacted]" : $"{value[..4]}...{value[^4..]}";
    private static string RedactMetadata(string value) => value.Length > 200 ? value[..200] + "…" : value;
    public sealed record AdminReason(string Reason);
}
