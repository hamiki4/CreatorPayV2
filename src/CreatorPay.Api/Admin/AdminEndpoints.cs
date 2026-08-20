using CreatorPay.Api.Authentication;
using CreatorPay.Application.Authentication;
using CreatorPay.Application.CustomerVerification;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

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
        admin.MapGet("/merchants/{merchantId:guid}/cashiers", MerchantCashiers);
        admin.MapGet("/cashiers", Cashiers);
        admin.MapGet("/accounts", Accounts);
        admin.MapPost("/accounts/create", CreateAccount);
        admin.MapGet("/password-reset-requests", PasswordResetRequests);
        admin.MapPost("/password-reset-requests/{id:guid}/approve", ApprovePasswordReset);
        admin.MapPost("/password-reset-requests/{id:guid}/reject", RejectPasswordReset);
        admin.MapGet("/reports/financial-summary", FinancialSummary).RequireRateLimiting("admin-report");
        admin.MapGet("/purchases", Purchases);
        admin.MapGet("/wallets", Wallets);
        admin.MapGet("/partnerships", Partnerships);
        admin.MapGet("/offline-sync", OfflineSync);
        admin.MapGet("/audit", Audit);
        admin.MapGet("/search", Search).RequireRateLimiting("admin-report");
        admin.MapGet("/support", Support).RequireRateLimiting("admin-report");
        admin.MapGet("/pilot-feedback", PilotFeedback).RequireRateLimiting("admin-report");
        admin.MapGet("/system", SystemStatus);
        admin.MapGet("/operational-alerts", Alerts);
        admin.MapPost("/accounts/{id:guid}/{action}", ChangeAccountStatus);
        admin.MapPost("/accounts/{id:guid}/revoke-sessions", RevokeSessions);
        admin.MapPost("/operational-alerts/{id:guid}/acknowledge", (Guid id, AdminReason request, HttpContext h, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => ChangeAlert(id, "Acknowledged", request.Reason, h, u, db, ct));
        admin.MapPost("/operational-alerts/{id:guid}/resolve", (Guid id, AdminReason request, HttpContext h, ICurrentUserService u, ApplicationDbContext db, CancellationToken ct) => ChangeAlert(id, "Resolved", request.Reason, h, u, db, ct));
        return endpoints;
    }

    private static async Task<IResult> PasswordResetRequests(ApplicationDbContext db, CancellationToken ct)
    {
        var items = await db.SupportRequests.AsNoTracking().Where(x => x.Subject == "Password Reset")
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => new { id = x.Id, name = x.Name, phone = x.Contact, role = x.UserType, requestedAtUtc = x.CreatedAtUtc, status = x.Status == "Approved" ? "Pending" : x.Status, canApprove = x.Status == "Pending" }).ToListAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> ApprovePasswordReset(Guid id, HttpContext h, ICurrentUserService current, ApplicationDbContext db, ITokenService tokens, IOptions<PasswordResetOptions> options, CancellationToken ct)
    {
        var request = await db.SupportRequests.SingleOrDefaultAsync(x => x.Id == id && x.Subject == "Password Reset", ct);
        if (request is null) return Results.NotFound();
        if (request.Status != "Pending") return Results.Conflict(new { detail = "This password reset request has already been reviewed." });
        if (!Guid.TryParse(request.Message, out var userId)) return Results.Problem("The request cannot be authorized.", statusCode: 409);
        var user = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == userId && x.Role != UserRole.PlatformAdmin, ct);
        if (user is null) return Results.Problem("The request cannot be authorized.", statusCode: 409);
        var now = DateTime.UtcNow;
        db.PasswordResetTokens.Add(new PasswordResetToken { Id = Guid.NewGuid(), UserAccountId = user.Id, TokenHash = tokens.HashToken(request.PublicReference), CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(options.Value.TokenLifetimeMinutes), RequestedByIp = h.Connection.RemoteIpAddress?.ToString() });
        request.Status = "Approved"; request.UpdatedAtUtc = now;
        await AddAudit(db, current.UserAccountId!.Value, "PasswordResetAuthorized", request.Id, "Admin approved password reset authorization", h.TraceIdentifier, ct);
        return Results.Ok(new { status = "Pending", authorized = true });
    }

    private static async Task<IResult> RejectPasswordReset(Guid id, HttpContext h, ICurrentUserService current, ApplicationDbContext db, CancellationToken ct)
    {
        var request = await db.SupportRequests.SingleOrDefaultAsync(x => x.Id == id && x.Subject == "Password Reset", ct);
        if (request is null) return Results.NotFound();
        if (request.Status != "Pending") return Results.Conflict(new { detail = "This password reset request has already been reviewed." });
        request.Status = "Rejected"; request.UpdatedAtUtc = DateTime.UtcNow;
        await AddAudit(db, current.UserAccountId!.Value, "PasswordResetRejected", request.Id, "Admin rejected password reset request", h.TraceIdentifier, ct);
        return Results.Ok(new { status = "Rejected" });
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
            activeCreators = await db.Creators.CountAsync(x => x.Status == CreatorStatus.Active, ct),
            activeBusinesses = await db.Merchants.CountAsync(x => x.Status == MerchantStatus.Active && (!merchantId.HasValue || x.Id == merchantId), ct),
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
            range = new { from = start, to = end },
            filters = new { merchantId, creatorId, locationId, offerId, status },
            pilotBusinesses = await db.Merchants.CountAsync(x => x.Status == MerchantStatus.Active, ct),
            pilotCreators = await db.Creators.CountAsync(x => x.Status == CreatorStatus.Active, ct),
            pilotCashiers = await db.Cashiers.CountAsync(x => x.IsActive && (!merchantId.HasValue || x.MerchantId == merchantId), ct),
            pilotShoppers = await db.Customers.CountAsync(x => x.Status == CustomerStatus.Active, ct),
            activePilotOffers = await db.CreatorMerchantCampaigns.CountAsync(x => x.Status == CampaignStatus.Active && (!merchantId.HasValue || x.MerchantId == merchantId) && (!creatorId.HasValue || x.CreatorId == creatorId), ct),
            todaysCheckouts = await sessions.CountAsync(ct),
            successfulCheckouts = await sessions.CountAsync(x => x.Status == CheckoutSessionStatus.Completed, ct),
            failedCheckouts = await sessions.CountAsync(x => x.Status == CheckoutSessionStatus.Rejected || x.Status == CheckoutSessionStatus.Expired || x.Status == CheckoutSessionStatus.Cancelled, ct),
            walletBalances = await db.MerchantWallets.Where(x => !merchantId.HasValue || x.MerchantId == merchantId).SumAsync(x => (decimal?)x.AvailableBalance, ct) ?? 0,
            lowBalanceBusinesses = await db.MerchantWallets.CountAsync(x => x.Status == MerchantWalletStatus.LowBalance && (!merchantId.HasValue || x.MerchantId == merchantId), ct),
            pendingCreatorEarnings = await db.CreatorEarnings.Where(x => x.Status == CreatorEarningStatus.Pending && (!creatorId.HasValue || x.CreatorId == creatorId)).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            pendingShopperCashback = await db.CustomerPayoutRequests.Where(x => x.Status == CustomerPayoutStatus.Requested || x.Status == CustomerPayoutStatus.Processing).SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            pendingPayouts = await db.CreatorPayouts.CountAsync(x => x.Status == CreatorPayoutStatus.Scheduled || x.Status == CreatorPayoutStatus.Processing || x.Status == CreatorPayoutStatus.Submitted, ct),
            failedNotifications = await db.NotificationOutboxMessages.CountAsync(x => x.Status == NotificationOutboxStatus.Failed || x.Status == NotificationOutboxStatus.DeadLettered, ct),
            supportRequests = await db.SupportRequests.CountAsync(x => x.Status == "Open", ct),
            disputes = await db.Disputes.CountAsync(x => x.Status == DisputeStatus.Open, ct),
            suspiciousActivity = await db.FraudAlerts.CountAsync(x => x.Status == FraudAlertStatus.Open, ct),
            serviceHealth = "API and database available",
            onboardingProgress = new { businessesApproved = await db.Merchants.CountAsync(x => x.Status == MerchantStatus.Active, ct), creatorsApproved = await db.Creators.CountAsync(x => x.Status == CreatorStatus.Active, ct), activePartnerships = await db.MerchantCreatorPartnerships.CountAsync(x => x.Status == PartnershipStatus.Approved, ct) },
            purchaseCount = await purchases.CountAsync(ct)
        });
    }

    private static async Task<IResult> Creators(string? q, CreatorStatus? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var query = db.Creators.AsNoTracking(); if (status.HasValue) query = query.Where(x => x.Status == status); if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(x => x.PublicCreatorId.Contains(q) || x.DisplayName.Contains(q) || x.Email.Contains(q)); } return Results.Ok(await Page(query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.PublicCreatorId, x.DisplayName, email = x.Email, phone = x.PhoneNumber, status = x.Status.ToString(), x.CreatedAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> Merchants(string? q, MerchantStatus? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var minimum = await db.PlatformFinancialSettings.AsNoTracking().Where(x => x.CurrencyCode == "ETB").Select(x => (decimal?)x.MinimumBusinessWalletBalance).SingleOrDefaultAsync(ct) ?? 0m; var query = db.Merchants.AsNoTracking(); if (status.HasValue) query = query.Where(x => x.Status == status); if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(x => x.PublicMerchantId.Contains(q) || x.TradingName.Contains(q) || (x.Email != null && x.Email.Contains(q))); } return Results.Ok(await Page(query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.PublicMerchantId, x.TradingName, email = x.Email, phone = x.PhoneNumber, status = x.Status.ToString(), walletBalance = db.MerchantWallets.Where(w => w.MerchantId == x.Id && w.CurrencyCode == "ETB").Select(w => (decimal?)w.AvailableBalance).FirstOrDefault() ?? 0m, requiredMinimum = minimum, fundingStatus = x.Status == MerchantStatus.Suspended ? "Suspended" : x.Status == MerchantStatus.Closed ? "Deactivated" : x.Status != MerchantStatus.Active ? "Pending Approval" : (db.MerchantWallets.Where(w => w.MerchantId == x.Id && w.CurrencyCode == "ETB").Select(w => (decimal?)w.AvailableBalance).FirstOrDefault() ?? 0m) < minimum ? "Insufficient Funds" : "Active", x.CreatedAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> Accounts(string? q, string? status, UserRole? role, string? business, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var now = DateTime.UtcNow; var minimum = await db.PlatformFinancialSettings.AsNoTracking().Where(x => x.CurrencyCode == "ETB").Select(x => (decimal?)x.MinimumBusinessWalletBalance).SingleOrDefaultAsync(ct) ?? 0m; var query = db.UserAccounts.AsNoTracking(); if (!string.IsNullOrWhiteSpace(status)) { if (status.Equals("Locked", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.LockoutEndUtc > now); else { var normalized = status.Equals("Pending", StringComparison.OrdinalIgnoreCase) ? AccountStatus.PendingVerification : status.Equals("Deactivated", StringComparison.OrdinalIgnoreCase) ? AccountStatus.Closed : Enum.TryParse<AccountStatus>(status, true, out var parsed) ? parsed : (AccountStatus?)null; if (!normalized.HasValue) return Results.BadRequest(new { detail = "Unknown account status filter." }); query = query.Where(x => x.Status == normalized.Value); } } if (role.HasValue) query = query.Where(x => x.Role == role); if (!string.IsNullOrWhiteSpace(q)) { var n = q.Trim().ToUpperInvariant(); var raw = q.Trim(); var digits = new string(raw.Where(char.IsDigit).ToArray()); var phoneSuffix = digits.StartsWith("0") ? digits[1..] : digits.StartsWith("251") ? digits[3..] : digits; var phoneFragment = digits.StartsWith("0") ? "251" + digits[1..] : digits; var hasPhone = phoneSuffix.Length > 0; query = query.Where(x => x.NormalizedEmail.Contains(n) || (hasPhone && x.NormalizedPhoneNumber != null && (x.NormalizedPhoneNumber.Contains(phoneFragment) || x.NormalizedPhoneNumber.EndsWith(phoneSuffix))) || (x.CreatorId.HasValue && db.Creators.Any(c => c.Id == x.CreatorId && (c.DisplayName.ToUpper().Contains(n) || (hasPhone && (c.PhoneNumber.Contains(phoneFragment) || c.PhoneNumber.EndsWith(phoneSuffix)))))) || (x.CustomerId.HasValue && db.Customers.Any(c => c.Id == x.CustomerId && (c.DisplayName.ToUpper().Contains(n) || (hasPhone && (c.PhoneNumber.Contains(phoneFragment) || c.PhoneNumber.EndsWith(phoneSuffix)))))) || (x.CashierId.HasValue && db.Cashiers.Any(c => c.Id == x.CashierId && ((c.FirstName + " " + c.LastName).ToUpper().Contains(n) || (hasPhone && (c.PhoneNumber.Contains(phoneFragment) || c.PhoneNumber.EndsWith(phoneSuffix))) || c.Merchant.TradingName.ToUpper().Contains(n) || c.Merchant.PublicMerchantId.ToUpper().Contains(n)))) || (x.MerchantId.HasValue && db.Merchants.Any(m => m.Id == x.MerchantId && (m.TradingName.ToUpper().Contains(n) || m.PublicMerchantId.ToUpper().Contains(n))))); } if (!string.IsNullOrWhiteSpace(business)) { var n = business.Trim().ToUpperInvariant(); query = query.Where(x => x.MerchantId.HasValue && db.Merchants.Any(m => m.Id == x.MerchantId && (m.TradingName.ToUpper().Contains(n) || m.PublicMerchantId.ToUpper().Contains(n)))); } return Results.Ok(await Page(query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, cashierName = x.CashierId.HasValue ? db.Cashiers.Where(c => c.Id == x.CashierId).Select(c => c.FirstName + " " + c.LastName).FirstOrDefault() : x.CreatorId.HasValue ? db.Creators.Where(c => c.Id == x.CreatorId).Select(c => c.DisplayName).FirstOrDefault() : x.CustomerId.HasValue ? db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.DisplayName).FirstOrDefault() : null, email = x.Email, phone = x.NormalizedPhoneNumber, businessName = x.MerchantId.HasValue ? db.Merchants.Where(m => m.Id == x.MerchantId).Select(m => m.TradingName).FirstOrDefault() : null, publicBusinessId = x.MerchantId.HasValue ? db.Merchants.Where(m => m.Id == x.MerchantId).Select(m => m.PublicMerchantId).FirstOrDefault() : null, assignedLocation = x.CashierId.HasValue ? db.CashierLocationAssignments.Where(a => a.CashierId == x.CashierId && a.IsActive).OrderByDescending(a => a.IsPrimary).Select(a => a.MerchantLocation.Name).FirstOrDefault() : null, role = x.Role.ToString(), status = x.Status.ToString(), x.IsEmailVerified, x.IsPhoneVerified, isLocked = x.LockoutEndUtc > now, x.LastLoginAtUtc, walletBalance = x.Role == UserRole.MerchantAdmin && x.MerchantId.HasValue ? db.MerchantWallets.Where(w => w.MerchantId == x.MerchantId && w.CurrencyCode == "ETB").Select(w => (decimal?)w.AvailableBalance).FirstOrDefault() : null, requiredMinimum = x.Role == UserRole.MerchantAdmin && x.MerchantId.HasValue ? minimum : (decimal?)null, fundingStatus = x.Role != UserRole.MerchantAdmin || !x.MerchantId.HasValue ? null : x.Status == AccountStatus.Suspended ? "Suspended" : x.Status == AccountStatus.Closed ? "Deactivated" : x.Status != AccountStatus.Active ? "Pending Approval" : (db.MerchantWallets.Where(w => w.MerchantId == x.MerchantId && w.CurrencyCode == "ETB").Select(w => (decimal?)w.AvailableBalance).FirstOrDefault() ?? 0m) < minimum ? "Insufficient Funds" : "Active" }), page, pageSize, ct)); }
    private static Task<IResult> Cashiers(int page, int pageSize, ApplicationDbContext db, CancellationToken ct) => CashierPage(null, page, pageSize, db, ct);
    private static Task<IResult> MerchantCashiers(Guid merchantId, int page, int pageSize, ApplicationDbContext db, CancellationToken ct) => CashierPage(merchantId, page, pageSize, db, ct);
    private static async Task<IResult> CashierPage(Guid? merchantId, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    {
        if (merchantId.HasValue && !await db.Merchants.AnyAsync(x => x.Id == merchantId, ct)) return Results.NotFound();
        var query = db.Cashiers.AsNoTracking().Where(x => !merchantId.HasValue || x.MerchantId == merchantId);
        return Results.Ok(await Page(query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).Select(x => new
        {
            x.Id,
            accountId = db.UserAccounts.Where(u => u.CashierId == x.Id).Select(u => (Guid?)u.Id).FirstOrDefault(),
            cashierName = x.FirstName + " " + x.LastName,
            email = x.Email,
            phone = x.PhoneNumber,
            businessName = x.Merchant.TradingName,
            publicBusinessId = x.Merchant.PublicMerchantId,
            assignedLocation = x.LocationAssignments.Where(a => a.IsActive).OrderByDescending(a => a.IsPrimary).Select(a => a.MerchantLocation.Name).FirstOrDefault(),
            status = db.UserAccounts.Where(u => u.CashierId == x.Id).Select(u => u.Status.ToString()).FirstOrDefault() ?? (x.IsActive ? "Active" : "Disabled"),
            lastLoginAtUtc = db.UserAccounts.Where(u => u.CashierId == x.Id).Select(u => u.LastLoginAtUtc).FirstOrDefault()
        }), page, pageSize, ct));
    }
    private static async Task<IResult> Purchases(string? q, Guid? merchantId, Guid? creatorId, TransactionStatus? status, DateTime? from, DateTime? to, int page, int pageSize, ApplicationDbContext db, CancellationToken ct)
    { var query = db.PurchaseTransactions.AsNoTracking(); if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.PublicTransactionId.Contains(q)); if (merchantId.HasValue) query = query.Where(x => x.MerchantId == merchantId); if (creatorId.HasValue) query = query.Where(x => x.CreatorId == creatorId); if (status.HasValue) query = query.Where(x => x.Status == status); if (from.HasValue) query = query.Where(x => x.TransactionDateUtc >= Normalize(from, null)); if (to.HasValue) query = query.Where(x => x.TransactionDateUtc <= Normalize(to, null)); return Results.Ok(await Page(query.OrderByDescending(x => x.TransactionDateUtc).Select(x => new { x.Id, x.PublicTransactionId, x.MerchantId, x.CreatorId, x.MerchantLocationId, x.CashierId, x.PurchaseAmount, x.CurrencyCode, status = x.Status.ToString(), x.TransactionDateUtc, x.CorrelationId, idempotencyKey = MaskKey(x.IdempotencyKey) }), page, pageSize, ct)); }
    private static async Task<IResult> Wallets(string? q, int page, int pageSize, ApplicationDbContext db, CancellationToken ct) { var minimum = await db.PlatformFinancialSettings.AsNoTracking().Where(x => x.CurrencyCode == "ETB").Select(x => (decimal?)x.MinimumBusinessWalletBalance).SingleOrDefaultAsync(ct) ?? 0m; var query = from w in db.MerchantWallets.AsNoTracking() join m in db.Merchants.AsNoTracking() on w.MerchantId equals m.Id select new { id = w.Id, business = m.TradingName, w.AvailableBalance, requiredMinimum = minimum, fundingStatus = m.Status == MerchantStatus.Suspended ? "Suspended" : m.Status == MerchantStatus.Closed ? "Deactivated" : m.Status != MerchantStatus.Active ? "Pending Approval" : w.AvailableBalance < minimum ? "Insufficient Funds" : "Active", lastDeposit = db.MerchantDeposits.Where(d => d.MerchantId == m.Id && d.Status == MerchantDepositStatus.Completed).Max(d => (DateTime?)d.VerifiedAtUtc), status = w.Status.ToString() }; if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.business.Contains(q)); return Results.Ok(await Page(query.OrderBy(x => x.business), page, pageSize, ct)); }
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
    private static async Task<IResult> PilotFeedback(string? q, string? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct) { var query = db.SupportRequests.AsNoTracking().Where(x => x.Subject.StartsWith("Pilot feedback:") || x.Subject.StartsWith("Checkout satisfaction:")); if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.PublicReference.Contains(q) || x.Subject.Contains(q)); if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status); return Results.Ok(await Page(query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.PublicReference, x.UserType, x.Subject, x.Message, x.PreferredLanguage, x.Status, x.CreatedAtUtc }), page, pageSize, ct)); }
    private static async Task<IResult> Alerts(string? status, int page, int pageSize, ApplicationDbContext db, CancellationToken ct) { var q = db.OperationalAlerts.AsNoTracking(); if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status); return Results.Ok(await Page(q.OrderByDescending(x => x.DetectedAtUtc).Select(x => new { x.Id, x.AlertType, x.Severity, x.Status, x.Title, x.SafeDescription, x.RelatedEntityType, x.RelatedEntityId, x.DetectedAtUtc, x.AcknowledgedAtUtc, x.ResolvedAtUtc, x.CorrelationId }), page, pageSize, ct)); }

    private static async Task<IResult> FinancialSummary(DateTime? from, DateTime? to, ApplicationDbContext db, CancellationToken ct)
    {
        var end = Normalize(to, DateTime.UtcNow); var start = Normalize(from, end.Date); if (start > end) return Results.BadRequest(new { error = "from must not be after to" });
        var purchases = db.PurchaseTransactions.AsNoTracking().Where(x => x.TransactionDateUtc >= start && x.TransactionDateUtc <= end && (x.Status == TransactionStatus.Confirmed || x.Status == TransactionStatus.Settled || x.Status == TransactionStatus.Disputed || x.Status == TransactionStatus.PartiallyReversed));
        var snapshots = from p in purchases join s in db.CommissionCalculationSnapshots.AsNoTracking() on p.CommissionCalculationSnapshotId equals s.Id select s;
        var approvedDeposits = db.MerchantDeposits.AsNoTracking().Where(x => x.Status == MerchantDepositStatus.Completed && x.VerifiedAtUtc >= start && x.VerifiedAtUtc <= end);
        return Results.Ok(new
        {
            range = new { from = start, to = end },
            totalSalesProcessed = await purchases.SumAsync(x => (decimal?)x.PurchaseAmount, ct) ?? 0m,
            totalCommissionReceived = await snapshots.SumAsync(x => (decimal?)x.TotalCommissionAmount, ct) ?? 0m,
            totalCreatorEarnings = await snapshots.SumAsync(x => (decimal?)x.CreatorCommissionAmount, ct) ?? 0m,
            totalShopperCashback = await snapshots.SumAsync(x => (decimal?)x.CustomerCashbackAmount, ct) ?? 0m,
            totalPlatformRevenue = await snapshots.SumAsync(x => (decimal?)x.PlatformCommissionAmount, ct) ?? 0m,
            totalPayoutsCompleted = (await db.CreatorPayouts.Where(x => x.Status == CreatorPayoutStatus.Paid && x.PaidAtUtc >= start && x.PaidAtUtc <= end).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m) + (await db.CustomerPayoutRequests.Where(x => x.Status == CustomerPayoutStatus.Paid && x.PaidAtUtc >= start && x.PaidAtUtc <= end).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m),
            outstandingPayouts = (await db.CreatorPayouts.Where(x => x.Status == CreatorPayoutStatus.Scheduled || x.Status == CreatorPayoutStatus.Processing || x.Status == CreatorPayoutStatus.Submitted).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m) + (await db.CustomerPayoutRequests.Where(x => x.Status == CustomerPayoutStatus.Requested || x.Status == CustomerPayoutStatus.Processing).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m),
            totalApprovedBusinessDeposits = await approvedDeposits.SumAsync(x => (decimal?)x.Amount, ct) ?? 0m,
            currentBusinessWalletFunds = await db.MerchantWallets.SumAsync(x => (decimal?)x.AvailableBalance, ct) ?? 0m
        });
    }

    private static async Task<IResult> CreateAccount(CreateAccountRequest request, HttpContext h, ICurrentUserService user, ApplicationDbContext db, IPasswordHasher passwords, PasswordPolicyValidator policy, CancellationToken ct)
    {
        if (user.UserAccountId is null) return Results.Problem(statusCode: 401, detail: "Authentication required.");
        if (request.Password != request.Confirmation) return Results.Problem(detail: "Password confirmation does not match.", statusCode: StatusCodes.Status400BadRequest);
        var passwordErrors = policy.Validate(request.Password); if (passwordErrors.Count > 0) return Results.Problem(detail: string.Join(" ", passwordErrors), statusCode: StatusCodes.Status400BadRequest);
        var now = DateTime.UtcNow; var actor = user.UserAccountId.Value;
        var email = CleanEmail(request.Email); var normalizedEmail = NormalizeEmail(email);
        var phone = CleanPhone(request.PhoneNumber); var normalizedPhone = phone is null ? string.Empty : EthiopianMobileNumber.Normalize(phone);
        if (string.IsNullOrWhiteSpace(email) && phone is null) return Results.Problem(detail: "Email or phone number is required.", statusCode: StatusCodes.Status400BadRequest);
        if (normalizedEmail.Length > 0 && await db.UserAccounts.AnyAsync(x => x.NormalizedEmail == normalizedEmail, ct)) return Results.Problem(detail: "Email is already registered.", statusCode: StatusCodes.Status409Conflict);
        if (phone is not null && await db.UserAccounts.AnyAsync(x => x.NormalizedPhoneNumber == normalizedPhone, ct)) return Results.Problem(detail: "Phone number is already registered.", statusCode: StatusCodes.Status409Conflict);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var createdAccounts = new List<Guid>();
        Guid? merchantId = request.MerchantId;
        try
        {
            switch (request.Role)
            {
                case UserRole.PlatformAdmin:
                {
                    if (string.IsNullOrWhiteSpace(email)) return Results.Problem(detail: "Email is required for PlatformAdmin.", statusCode: StatusCodes.Status400BadRequest);
                    var account = new UserAccount { Id = Guid.NewGuid(), Email = email, NormalizedEmail = normalizedEmail, PhoneNumber = phone, NormalizedPhoneNumber = phone is null ? null : normalizedPhone, Role = UserRole.PlatformAdmin, Status = AccountStatus.Active, IsEmailVerified = true, IsPhoneVerified = phone is not null, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                    account.PasswordHash = passwords.Hash(account, request.Password);
                    db.Add(account);
                    createdAccounts.Add(account.Id);
                    break;
                }
                case UserRole.Customer:
                {
                    if (string.IsNullOrWhiteSpace(request.DisplayName)) return Results.Problem(detail: "Display name is required.", statusCode: StatusCodes.Status400BadRequest);
                    if (phone is null) return Results.Problem(detail: "Phone number is required.", statusCode: StatusCodes.Status400BadRequest);
                    var customer = new Customer { Id = Guid.NewGuid(), PublicCustomerId = $"CUS-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), DisplayName = request.DisplayName.Trim(), PhoneNumber = phone, NormalizedPhoneNumber = normalizedPhone, Status = CustomerStatus.Active, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                    db.Add(customer);
                    var account = new UserAccount { Id = Guid.NewGuid(), Email = email ?? string.Empty, NormalizedEmail = normalizedEmail, PhoneNumber = phone, NormalizedPhoneNumber = normalizedPhone, Role = UserRole.Customer, Status = AccountStatus.Active, CustomerId = customer.Id, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                    account.PasswordHash = passwords.Hash(account, request.Password);
                    db.Add(account);
                    createdAccounts.Add(account.Id);
                    break;
                }
                case UserRole.Creator:
                {
                    if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || string.IsNullOrWhiteSpace(request.DisplayName)) return Results.Problem(detail: "First name, last name, and display name are required.", statusCode: StatusCodes.Status400BadRequest);
                    if (phone is null) return Results.Problem(detail: "Phone number is required.", statusCode: StatusCodes.Status400BadRequest);
                    var creatorId = Guid.NewGuid();
                    var creatorCode = Random.Shared.Next(1000, 9999).ToString();
                    while (await db.Creators.AnyAsync(x => x.CreatorCode == creatorCode, ct)) creatorCode = Random.Shared.Next(1000, 9999).ToString();
                    var creator = new Creator { Id = creatorId, PublicCreatorId = $"CRE-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), CreatorCode = creatorCode, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DisplayName = request.DisplayName.Trim(), PhoneNumber = phone, NormalizedPhoneNumber = normalizedPhone, Email = email ?? string.Empty, PreferredLanguage = CleanValue(request.PreferredLanguage, "en"), City = CleanValue(request.City, "Addis Ababa"), Zone = CleanOptional(request.Zone), Biography = CleanValue(request.Biography, string.Empty), ContentCategories = CleanValue(request.ContentCategories, string.Empty), TermsAcceptedAtUtc = now, Status = CreatorStatus.PendingApproval, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                    db.Add(creator);
                    creator.Approve(now, actor);
                    var account = new UserAccount { Id = Guid.NewGuid(), Email = email ?? string.Empty, NormalizedEmail = normalizedEmail, PhoneNumber = phone, NormalizedPhoneNumber = normalizedPhone, Role = UserRole.Creator, Status = AccountStatus.Active, CreatorId = creator.Id, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                    account.PasswordHash = passwords.Hash(account, request.Password);
                    db.Add(account);
                    createdAccounts.Add(account.Id);
                    break;
                }
                case UserRole.MerchantAdmin:
                {
                    if (merchantId is null)
                    {
                        var merchantError = ValidateBusinessFields(request);
                        if (merchantError is not null) return Results.Problem(detail: merchantError, statusCode: StatusCodes.Status400BadRequest);
                        if (phone is null) return Results.Problem(detail: "Phone number is required.", statusCode: StatusCodes.Status400BadRequest);
                        var merchant = new Merchant { Id = Guid.NewGuid(), PublicMerchantId = $"ME-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), LegalBusinessName = CleanValue(request.LegalBusinessName, request.TradingName ?? string.Empty), TradingName = CleanValue(request.TradingName, "Business"), BusinessType = CleanValue(request.BusinessType, "Other"), PrimaryContactName = CleanValue(request.PrimaryContactName, email ?? "Platform Admin"), PhoneNumber = phone, NormalizedPhoneNumber = normalizedPhone, Email = email, BusinessAddress = CleanValue(request.BusinessAddress, "Not provided"), City = CleanValue(request.City, "Addis Ababa"), Region = CleanValue(request.Region, "Not provided"), Country = CleanValue(request.Country, "Ethiopia"), TimeZone = CleanValue(request.TimeZone, "Africa/Addis_Ababa"), PreferredLanguage = CleanValue(request.PreferredLanguage, "en"), TermsAcceptedAtUtc = now, Status = MerchantStatus.PendingReview, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                        db.Add(merchant);
                        merchant.Approve(now, actor);
                        merchantId = merchant.Id;
                    }
                    else
                    {
                        var merchant = await db.Merchants.SingleOrDefaultAsync(x => x.Id == merchantId, ct);
                        if (merchant is null) return Results.Problem(detail: "Business not found.", statusCode: StatusCodes.Status404NotFound);
                        if (merchant.Status is not (MerchantStatus.Active or MerchantStatus.LowBalance or MerchantStatus.LowBalanceRestricted or MerchantStatus.ApprovedUnfunded)) return Results.Problem(detail: "Business must be active before assigning a PlatformAdmin-created account.", statusCode: StatusCodes.Status409Conflict);
                    }
                    var owner = new UserAccount { Id = Guid.NewGuid(), Email = email ?? string.Empty, NormalizedEmail = normalizedEmail, PhoneNumber = phone, NormalizedPhoneNumber = phone is null ? null : normalizedPhone, Role = UserRole.MerchantAdmin, Status = AccountStatus.Active, MerchantId = merchantId, IsEmailVerified = true, IsPhoneVerified = phone is not null, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                    owner.PasswordHash = passwords.Hash(owner, request.Password);
                    db.Add(owner);
                    createdAccounts.Add(owner.Id);
                    break;
                }
                case UserRole.Cashier:
                {
                    if (merchantId is null) return Results.Problem(detail: "Business is required for a Cashier.", statusCode: StatusCodes.Status400BadRequest);
                    var merchant = await db.Merchants.SingleOrDefaultAsync(x => x.Id == merchantId, ct);
                    if (merchant is null) return Results.Problem(detail: "Business not found.", statusCode: StatusCodes.Status404NotFound);
                    if (merchant.Status is not (MerchantStatus.Active or MerchantStatus.LowBalance or MerchantStatus.LowBalanceRestricted or MerchantStatus.ApprovedUnfunded)) return Results.Problem(detail: "Business must be active before assigning a Cashier.", statusCode: StatusCodes.Status409Conflict);
                    if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName)) return Results.Problem(detail: "First name and last name are required.", statusCode: StatusCodes.Status400BadRequest);
                    if (phone is null) return Results.Problem(detail: "Phone number is required.", statusCode: StatusCodes.Status400BadRequest);
                    var cashier = new Cashier { Id = Guid.NewGuid(), MerchantId = merchant.Id, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Email = email ?? string.Empty, NormalizedEmail = normalizedEmail, PhoneNumber = phone, NormalizedPhoneNumber = normalizedPhone, IsActive = true, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                    db.Add(cashier);
                    var account = new UserAccount { Id = Guid.NewGuid(), Email = email ?? string.Empty, NormalizedEmail = normalizedEmail, PhoneNumber = phone, NormalizedPhoneNumber = normalizedPhone, Role = UserRole.Cashier, Status = AccountStatus.Active, MerchantId = merchant.Id, CashierId = cashier.Id, IsEmailVerified = true, IsPhoneVerified = true, CreatedAtUtc = now, CreatedBy = actor.ToString() };
                    account.PasswordHash = passwords.Hash(account, request.Password);
                    db.Add(account);
                    createdAccounts.Add(account.Id);
                    break;
                }
                default:
                    return Results.Problem(detail: "Unsupported account role.", statusCode: StatusCodes.Status400BadRequest);
            }

            db.OperationalAuditEvents.Add(new OperationalAuditEvent
            {
                Id = Guid.NewGuid(),
                EventType = "AdminAccountCreated",
                ActorUserId = actor,
                MerchantId = merchantId,
                SubjectId = createdAccounts.Single(),
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { role = request.Role.ToString(), createdAccountId = createdAccounts.Single(), merchantId, createdAtUtc = now, actorUserId = actor }),
                CorrelationId = h.TraceIdentifier,
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/v1/admin/accounts/{createdAccounts.Single()}", new { accountId = createdAccounts.Single(), role = request.Role.ToString(), merchantId });
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            return Results.Problem(detail: ex.InnerException?.Message ?? ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Account creation failed");
        }
    }

    private static async Task<IResult> UnlockAccount(Guid id, AdminReason request, HttpContext h, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct) { var account = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id, ct); if (account is null) return Results.NotFound(); account.FailedLoginCount = 0; account.LockoutEndUtc = null; await AddAudit(db, user.UserAccountId!.Value, "AdminAccountUnlocked", id, request.Reason, h.TraceIdentifier, ct); return Results.NoContent(); }
    private static async Task<IResult> ChangeAccountStatus(Guid id, string action, AdminReason request, HttpContext h, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct)
    {
        var account = await db.UserAccounts.SingleOrDefaultAsync(x => x.Id == id, ct); if (account is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.Reason)) return Results.BadRequest(new { error = "A reason is required." });
        var normalized = action.Trim().ToLowerInvariant(); var before = account.Status;
        if (normalized == "lock") { account.LockoutEndUtc = DateTime.UtcNow.AddYears(100); account.FailedLoginCount = Math.Max(account.FailedLoginCount, 5); }
        else if (normalized == "unlock") { account.LockoutEndUtc = null; account.FailedLoginCount = 0; }
        else if (normalized is "suspend" or "deactivate") account.Status = normalized == "deactivate" ? AccountStatus.Closed : AccountStatus.Suspended;
        else if (normalized == "reactivate" && account.Status is AccountStatus.Suspended or AccountStatus.Closed) account.Status = AccountStatus.Active;
        else if (normalized == "reactivate") return Results.Conflict(new { error = "Only a suspended or deactivated approved account can be reactivated." });
        else return Results.BadRequest(new { error = "Unsupported account action." });
        if (account.CreatorId.HasValue && normalized is "suspend" or "deactivate" or "reactivate") { var creator = await db.Creators.SingleAsync(x => x.Id == account.CreatorId, ct); creator.Status = normalized == "reactivate" ? CreatorStatus.Active : normalized == "deactivate" ? CreatorStatus.Closed : CreatorStatus.Suspended; creator.UpdatedAtUtc = DateTime.UtcNow; }
        if (account.Role == UserRole.MerchantAdmin && account.MerchantId.HasValue && normalized is "suspend" or "deactivate" or "reactivate") { var merchant = await db.Merchants.SingleAsync(x => x.Id == account.MerchantId, ct); merchant.Status = normalized == "reactivate" ? MerchantStatus.Active : normalized == "deactivate" ? MerchantStatus.Closed : MerchantStatus.Suspended; merchant.UpdatedAtUtc = DateTime.UtcNow; }
        if (account.Role == UserRole.Cashier && account.CashierId.HasValue && normalized is "suspend" or "deactivate" or "reactivate") { var cashier = await db.Cashiers.SingleAsync(x => x.Id == account.CashierId, ct); cashier.IsActive = normalized == "reactivate"; cashier.UpdatedAtUtc = DateTime.UtcNow; }
        account.UpdatedAtUtc = DateTime.UtcNow; await AddAudit(db, user.UserAccountId!.Value, $"AdminAccount{char.ToUpperInvariant(normalized[0]) + normalized[1..]}", id, $"{request.Reason}; PreviousStatus={before}; NewStatus={account.Status}", h.TraceIdentifier, ct); return Results.NoContent();
    }
    private static async Task<IResult> RevokeSessions(Guid id, AdminReason request, HttpContext h, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct) { if (!await db.UserAccounts.AnyAsync(x => x.Id == id, ct)) return Results.NotFound(); var tokens = await db.RefreshTokens.Where(x => x.UserAccountId == id && x.RevokedAtUtc == null).ToListAsync(ct); foreach (var token in tokens) token.RevokedAtUtc = DateTime.UtcNow; await AddAudit(db, user.UserAccountId!.Value, "AdminSessionsRevoked", id, request.Reason, h.TraceIdentifier, ct); return Results.NoContent(); }
    private static async Task<IResult> ChangeAlert(Guid id, string status, string reason, HttpContext h, ICurrentUserService user, ApplicationDbContext db, CancellationToken ct) { var alert = await db.OperationalAlerts.SingleOrDefaultAsync(x => x.Id == id, ct); if (alert is null) return Results.NotFound(); var old = alert.Status; alert.Status = status; alert.UpdatedAtUtc = DateTime.UtcNow; if (status == "Acknowledged") { alert.AcknowledgedAtUtc = DateTime.UtcNow; alert.AcknowledgedByUserId = user.UserAccountId; } if (status == "Resolved") alert.ResolvedAtUtc = DateTime.UtcNow; db.OperationalAlertHistories.Add(new() { Id = Guid.NewGuid(), OperationalAlertId = id, PreviousStatus = old, NewStatus = status, ActorUserId = user.UserAccountId!.Value, Reason = reason, ChangedAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow }); await AddAudit(db, user.UserAccountId.Value, $"OperationalAlert{status}", id, reason, h.TraceIdentifier, ct); return Results.NoContent(); }
    private static async Task AddAudit(ApplicationDbContext db, Guid actor, string eventType, Guid subject, string reason, string correlation, CancellationToken ct) { db.OperationalAuditEvents.Add(new() { Id = Guid.NewGuid(), EventType = eventType, ActorUserId = actor, SubjectId = subject, MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { reason }), CorrelationId = correlation, CreatedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(ct); }
    private static async Task<object> Page<T>(IQueryable<T> query, int page, int pageSize, CancellationToken ct) { page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, MaxPageSize); var total = await query.CountAsync(ct); var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); return new { items, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) }; }
    private static DateTime Normalize(DateTime? value, DateTime? fallback) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : fallback!.Value;
    private static string CleanEmail(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    private static string? CleanPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return EthiopianMobileNumber.Normalize(value); } catch (ArgumentException) { return null; }
    }
    private static string NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
    private static string CleanValue(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? ValidateBusinessFields(CreateAccountRequest request) =>
        string.IsNullOrWhiteSpace(request.TradingName) ? "Trading name is required when creating a new Business." :
        string.IsNullOrWhiteSpace(request.BusinessType) ? "Business type is required when creating a new Business." :
        string.IsNullOrWhiteSpace(request.PrimaryContactName) ? "Primary contact name is required when creating a new Business." :
        string.IsNullOrWhiteSpace(request.BusinessAddress) ? "Business address is required when creating a new Business." :
        string.IsNullOrWhiteSpace(request.City) ? "City is required when creating a new Business." :
        string.IsNullOrWhiteSpace(request.Region) ? "Region is required when creating a new Business." :
        string.IsNullOrWhiteSpace(request.Country) ? "Country is required when creating a new Business." :
        string.IsNullOrWhiteSpace(request.TimeZone) ? "Time zone is required when creating a new Business." : null;
    private static string MaskPhone(string value) => value.Length <= 4 ? "****" : $"***-***-{value[^4..]}";
    private static string MaskEmail(string value) { var at = value.IndexOf('@'); return at <= 1 ? "***" : $"{value[0]}***{value[(at - 1)..]}"; }
    private static string MaskKey(string value) => value.Length < 9 ? "[redacted]" : $"{value[..4]}...{value[^4..]}";
    private static string RedactMetadata(string value) => value.Length > 200 ? value[..200] + "…" : value;
    public sealed record AdminReason(string Reason);
    public sealed record CreateAccountRequest(UserRole Role, string? Email, string? PhoneNumber, string Password, string Confirmation, string? FirstName = null, string? LastName = null, string? DisplayName = null, string? LegalBusinessName = null, string? TradingName = null, string? BusinessType = null, string? PrimaryContactName = null, string? BusinessAddress = null, string? City = null, string? Region = null, string? Country = null, string? TimeZone = null, string? PreferredLanguage = "en", string? Biography = null, string? ContentCategories = null, string? Zone = null, Guid? MerchantId = null);
}
