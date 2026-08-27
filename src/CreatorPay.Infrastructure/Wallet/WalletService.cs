using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CreatorPay.Application.Commission;
using CreatorPay.Application.Wallet;
using CreatorPay.Application.Earnings;
using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;
using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Infrastructure.Eligibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CreatorPay.Application.CustomerVerification;

namespace CreatorPay.Infrastructure.Wallet;

public sealed class WalletService(ApplicationDbContext db, IOptions<WalletOptions> options) : IWalletService
{
    private readonly WalletOptions settings = options.Value;
    public async Task<WalletDto> GetWalletAsync(Guid merchantId, CancellationToken ct)
    {
        var wallet = await EnsureWallet(merchantId, ct);
        var minimum = await BusinessWalletMinimumQueries.CurrentMinimumAsync(db, settings.CurrencyCode, merchantId, ct);
        var eligible = await db.Merchants.AsNoTracking().AnyAsync(x => x.Id == merchantId && x.Status == MerchantStatus.Active, ct) && wallet.AvailableBalance >= minimum;
        return Map(wallet, minimum, eligible);
    }
    public async Task<IReadOnlyList<WalletEntryDto>> GetEntriesAsync(Guid merchantId, CancellationToken ct) => await db.MerchantWalletEntries.AsNoTracking().Where(x => x.MerchantId == merchantId).OrderByDescending(x => x.CreatedAtUtc).Select(x => new WalletEntryDto(x.Id, x.EntryType.ToString(), x.Amount, x.BalanceBefore, x.BalanceAfter, x.Description, x.CreatedAtUtc)).ToListAsync(ct);
    public async Task<IReadOnlyList<DepositDto>> GetDepositsAsync(Guid? merchantId, bool pendingOnly, CancellationToken ct) { var q = db.MerchantDeposits.AsNoTracking().AsQueryable(); if (merchantId.HasValue) q = q.Where(x => x.MerchantId == merchantId); if (pendingOnly) q = q.Where(x => x.Status == MerchantDepositStatus.PendingVerification); return await (from x in q join m in db.Merchants.AsNoTracking() on x.MerchantId equals m.Id orderby x.SubmittedAtUtc descending select new DepositDto(x.Id, x.MerchantId, m.TradingName, x.Amount, x.CurrencyCode, x.Status.ToString(), x.ExternalReference, x.ProofMetadata, x.SubmittedAtUtc, x.VerifiedAtUtc, x.FailureReason)).ToListAsync(ct); }
    public async Task<DepositDto> GetDepositAsync(Guid id, Guid? merchantId, CancellationToken ct) => await (from x in db.MerchantDeposits.AsNoTracking() join m in db.Merchants.AsNoTracking() on x.MerchantId equals m.Id where x.Id == id && (!merchantId.HasValue || x.MerchantId == merchantId) select new DepositDto(x.Id, x.MerchantId, m.TradingName, x.Amount, x.CurrencyCode, x.Status.ToString(), x.ExternalReference, x.ProofMetadata, x.SubmittedAtUtc, x.VerifiedAtUtc, x.FailureReason)).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Deposit was not found.");
    public async Task<DepositDto> SubmitDepositAsync(Guid merchantId, Guid actor, string key, SubmitDepositRequest r, CancellationToken ct) { RequireKey(key); if (r.Amount <= 0) throw new ArgumentException("Deposit amount must be positive."); if (string.IsNullOrWhiteSpace(r.ProofMetadata)) throw new ArgumentException("Payment proof is required."); var currency = Currency(r.CurrencyCode); var hash = Hash(r); var old = await db.MerchantDeposits.AsNoTracking().SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.IdempotencyKey == key, ct); if (old is not null) { if (old.RequestHash != hash) throw new IdempotencyConflictException(); return Map(old, await BusinessName(old.MerchantId, ct)); } var wallet = await EnsureWallet(merchantId, ct); var now = DateTime.UtcNow; var d = new MerchantDeposit { Id = Guid.NewGuid(), MerchantId = merchantId, MerchantWalletId = wallet.Id, Amount = r.Amount, CurrencyCode = currency, Status = MerchantDepositStatus.PendingVerification, ExternalReference = r.ExternalReference.Trim(), ProofMetadata = r.ProofMetadata, SubmittedAtUtc = now, IdempotencyKey = key, RequestHash = hash, CreatedAtUtc = now, CreatedBy = actor.ToString() }; db.Add(d); Audit(merchantId, actor, "DepositSubmitted", $"DepositId={d.Id}"); await db.SaveChangesAsync(ct); return Map(d, await BusinessName(merchantId, ct)); }
    public async Task<DepositDto> ApproveDepositAsync(Guid id, Guid actor, string key, CancellationToken ct) { RequireKey(key); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); var d = await db.MerchantDeposits.Include(x => x.Wallet).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); if (d.Status == MerchantDepositStatus.Completed) return Map(d, await BusinessName(d.MerchantId, ct)); if (d.Status != MerchantDepositStatus.PendingVerification) throw new InvalidOperationException("Only a pending deposit can be approved."); var now = DateTime.UtcNow; var balance = d.Wallet.Credit(d.Amount, settings.LowBalanceThreshold, now); d.Status = MerchantDepositStatus.Completed; d.VerifiedAtUtc = now; d.VerifiedByUserId = actor; db.Add(Entry(d.Wallet, d.MerchantId, d.Amount, balance, d.Id, actor, key, MerchantWalletEntryType.Deposit)); db.Add(Journal(now, $"DEP-{d.Id}", null, d.Id, (JournalAccount.PaymentClearing, JournalLineType.Debit, d.Amount), (JournalAccount.MerchantWalletLiability, JournalLineType.Credit, d.Amount))); Audit(d.MerchantId, actor, "DepositApproved", $"DepositId={d.Id}"); Audit(d.MerchantId, actor, "WalletCredited", $"BalanceAfter={balance.After}"); var merchant = await db.Merchants.SingleAsync(x => x.Id == d.MerchantId, ct); var minimum = await BusinessWalletMinimumQueries.CurrentMinimumAsync(db, d.CurrencyCode, d.MerchantId, ct); var transition = merchant.EvaluateFunding(balance.After, minimum, settings.LowBalanceThreshold, now, actor.ToString()); if (transition != "Unchanged") Audit(d.MerchantId, actor, transition.EndsWith("Active", StringComparison.Ordinal) ? "MerchantReactivated" : "MerchantFundingStateChanged", transition); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(d, merchant.TradingName); }
    public async Task<DepositDto> RejectDepositAsync(Guid id, Guid actor, RejectDepositRequest r, CancellationToken ct) { if (string.IsNullOrWhiteSpace(r.Reason)) throw new ArgumentException("A rejection reason is required."); var d = await db.MerchantDeposits.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException(); if (d.Status != MerchantDepositStatus.PendingVerification) throw new InvalidOperationException("Only a pending deposit can be rejected."); d.Status = MerchantDepositStatus.Failed; d.FailureReason = r.Reason.Trim(); d.VerifiedAtUtc = DateTime.UtcNow; d.VerifiedByUserId = actor; Audit(d.MerchantId, actor, "DepositRejected", $"DepositId={d.Id}"); await db.SaveChangesAsync(ct); return Map(d, await BusinessName(d.MerchantId, ct)); }
    public async Task<PurchaseDto> GetPurchaseAsync(Guid id, Guid? merchantId, Guid? cashierId, CancellationToken ct) { var p = await Query().SingleOrDefaultAsync(x => x.Id == id && (!merchantId.HasValue || x.MerchantId == merchantId) && (!cashierId.HasValue || x.CashierId == cashierId), ct) ?? throw new KeyNotFoundException(); return Map(p, p.CommissionSnapshot, p.CreatorId.ToString(), p.MerchantLocationId?.ToString() ?? "Not assigned", null); }
    public async Task<IReadOnlyList<PurchaseDto>> GetPurchasesAsync(Guid? merchantId, Guid? cashierId, int take, CancellationToken ct) { var q = Query(); if (merchantId.HasValue) q = q.Where(x => x.MerchantId == merchantId); if (cashierId.HasValue) q = q.Where(x => x.CashierId == cashierId); return (await q.OrderByDescending(x => x.TransactionDateUtc).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct)).Select(x => Map(x, x.CommissionSnapshot, x.CreatorId.ToString(), x.MerchantLocationId?.ToString() ?? "Not assigned", null)).ToList(); }
    public async Task<ConfirmedSalesReportDto> GetConfirmedSalesAsync(Guid merchantId, ConfirmedSalesQuery request, CancellationToken ct)
    {
        var q = db.PurchaseTransactions.AsNoTracking().Where(x => x.MerchantId == merchantId && (x.Status == TransactionStatus.Confirmed || x.Status == TransactionStatus.Settled));
        if (request.CreatorId.HasValue) q = q.Where(x => x.CreatorId == request.CreatorId);
        if (request.CashierId.HasValue) q = q.Where(x => x.CashierId == request.CashierId);
        if (!string.IsNullOrWhiteSpace(request.CreatorName))
        {
            var term = request.CreatorName.Trim();
            q = q.Where(x => db.Creators.Any(c => c.Id == x.CreatorId && EF.Functions.ILike(c.DisplayName, $"%{term}%")));
        }
        if (!string.IsNullOrWhiteSpace(request.CashierName))
        {
            var term = request.CashierName.Trim();
            q = q.Where(x => x.CashierId.HasValue
                ? db.Cashiers.Any(c => c.Id == x.CashierId && (EF.Functions.ILike(c.FirstName, $"%{term}%") || EF.Functions.ILike(c.LastName, $"%{term}%")))
                : db.Merchants.Any(m => m.Id == x.MerchantId && (EF.Functions.ILike(m.PrimaryContactName, $"%{term}%") || EF.Functions.ILike(m.TradingName, $"%{term}%"))));
        }
        if (request.DateFromUtc.HasValue) q = q.Where(x => x.TransactionDateUtc >= request.DateFromUtc.Value);
        if (request.DateToUtc.HasValue) q = q.Where(x => x.TransactionDateUtc < request.DateToUtc.Value);
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<TransactionStatus>(request.Status, true, out var status)) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(x => EF.Functions.ILike(x.PublicTransactionId, $"%{term}%") ||
                db.Creators.Any(c => c.Id == x.CreatorId && (EF.Functions.ILike(c.DisplayName, $"%{term}%") || EF.Functions.ILike(c.CreatorCode, $"%{term}%") || EF.Functions.ILike(c.PublicCreatorId, $"%{term}%"))) ||
                (x.CashierId.HasValue ? db.Cashiers.Any(c => c.Id == x.CashierId && (EF.Functions.ILike(c.FirstName, $"%{term}%") || EF.Functions.ILike(c.LastName, $"%{term}%"))) : db.Merchants.Any(m => m.Id == x.MerchantId && (EF.Functions.ILike(m.PrimaryContactName, $"%{term}%") || EF.Functions.ILike(m.TradingName, $"%{term}%")))));
        }
        var all = await q.OrderByDescending(x => x.TransactionDateUtc).ToListAsync(ct);
        var snapshots = await db.CommissionCalculationSnapshots.AsNoTracking().Where(x => all.Select(p => p.CommissionCalculationSnapshotId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var creatorIds = all.Select(x => x.CreatorId).Distinct().ToArray();
        var creators = await db.Creators.AsNoTracking().Where(x => creatorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var cashierIds = all.Where(x => x.CashierId.HasValue).Select(x => x.CashierId!.Value).Distinct().ToArray();
        var cashiers = await db.Cashiers.AsNoTracking().Where(x => cashierIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var merchantIds = all.Select(x => x.MerchantId).Distinct().ToArray();
        var merchants = await db.Merchants.AsNoTracking().Where(x => merchantIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var locationIds = all.Where(x => x.MerchantLocationId.HasValue).Select(x => x.MerchantLocationId!.Value).Distinct().ToArray();
        var locations = await db.MerchantLocations.AsNoTracking().Where(x => locationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var rows = all.Skip(Math.Max(0, request.Page - 1) * Math.Clamp(request.PageSize, 1, 100)).Take(Math.Clamp(request.PageSize, 1, 100)).Select(x =>
        {
            var creator = creators[x.CreatorId]; var merchant = merchants[x.MerchantId]; var snapshot = snapshots[x.CommissionCalculationSnapshotId];
            var cashierName = x.CashierId.HasValue && cashiers.TryGetValue(x.CashierId.Value, out var cashier) ? $"{cashier.FirstName} {cashier.LastName}".Trim() : string.IsNullOrWhiteSpace(merchant.PrimaryContactName) ? "Business Owner" : $"{merchant.PrimaryContactName} (Business Owner)";
            return new ConfirmedSaleDto(x.Id, x.PublicTransactionId, x.ConfirmedAtUtc ?? x.TransactionDateUtc, x.PurchaseAmount, snapshot.TotalCommissionAmount, creator.DisplayName, string.IsNullOrWhiteSpace(creator.CreatorCode) ? creator.PublicCreatorId : creator.CreatorCode, cashierName, x.Status.ToString(), (x.MerchantLocationId.HasValue ? locations.GetValueOrDefault(x.MerchantLocationId.Value)?.Name : null));
        }).ToList();
        var performance = all.GroupBy(x => x.CreatorId).Select(g => { var c = creators[g.Key]; return new CreatorPerformanceDto(c.DisplayName, string.IsNullOrWhiteSpace(c.CreatorCode) ? c.PublicCreatorId : c.CreatorCode, g.Count(), g.Sum(x => x.PurchaseAmount), g.Sum(x => snapshots[x.CommissionCalculationSnapshotId].TotalCommissionAmount)); }).OrderByDescending(x => x.SalesAmount).ToList();
        return new ConfirmedSalesReportDto(all.Count, all.Sum(x => x.PurchaseAmount), all.Sum(x => snapshots[x.CommissionCalculationSnapshotId].TotalCommissionAmount), rows, performance);
    }
    private IQueryable<PurchaseTransaction> Query() => db.PurchaseTransactions.AsNoTracking().Include(x => x.CommissionSnapshot);
    private async Task<MerchantWallet> EnsureWallet(Guid merchantId, CancellationToken ct) { var w = await db.MerchantWallets.SingleOrDefaultAsync(x => x.MerchantId == merchantId && x.CurrencyCode == settings.CurrencyCode, ct); if (w is not null) return w; var m = await db.Merchants.FindAsync([merchantId], ct) ?? throw new KeyNotFoundException(); if (m.Status is MerchantStatus.Rejected or MerchantStatus.Closed) throw new InvalidOperationException("Merchant is not eligible for a wallet."); w = new() { Id = Guid.NewGuid(), MerchantId = merchantId, CurrencyCode = settings.CurrencyCode, CreatedAtUtc = DateTime.UtcNow }; db.Add(w); Audit(merchantId, null, "WalletCreated", "Currency=ETB"); await db.SaveChangesAsync(ct); return w; }
    private void Audit(Guid merchant, Guid? actor, string type, string detail) => db.MerchantAuditEvents.Add(new() { Id = Guid.NewGuid(), MerchantId = merchant, ActorUserAccountId = actor, EventType = type, Detail = detail, CreatedAtUtc = DateTime.UtcNow, CreatedBy = actor?.ToString() });
    private static MerchantWalletEntry Entry(MerchantWallet w, Guid merchant, decimal amount, (decimal Before, decimal After) b, Guid? deposit, Guid actor, string key, MerchantWalletEntryType type, Guid? transaction = null) => new() { Id = Guid.NewGuid(), MerchantWalletId = w.Id, MerchantId = merchant, EntryType = type, Amount = amount, CurrencyCode = w.CurrencyCode, BalanceBefore = b.Before, BalanceAfter = b.After, RelatedDepositId = deposit, RelatedTransactionId = transaction, IdempotencyKey = key, Description = type.ToString(), CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = actor, CreatedBy = actor.ToString(), CorrelationId = key };
    private static FinancialJournal Journal(DateTime now, string reference, Guid? transaction, Guid? deposit, params (JournalAccount Account, JournalLineType Type, decimal Amount)[] lines) { var j = new FinancialJournal { Id = Guid.NewGuid(), Reference = reference, Description = reference, RelatedTransactionId = transaction, RelatedDepositId = deposit, CreatedAtUtc = now }; foreach (var x in lines) j.Lines.Add(new() { Id = Guid.NewGuid(), Account = x.Account, Type = x.Type, Amount = x.Amount, CurrencyCode = "ETB", Description = reference, CreatedAtUtc = now }); j.Post(now); return j; }
    private static string Hash<T>(T x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(x)))); private static string Currency(string x) { var c = x.Trim().ToUpperInvariant(); if (c != "ETB") throw new ArgumentException("Only ETB is supported."); return c; }
    private static void RequireKey(string x) { if (string.IsNullOrWhiteSpace(x) || x.Length > 200) throw new ArgumentException("A valid Idempotency-Key header is required."); }
    private Task<string> BusinessName(Guid id, CancellationToken ct) => db.Merchants.Where(x => x.Id == id).Select(x => x.TradingName).SingleAsync(ct);
    private static WalletDto Map(MerchantWallet x, decimal minimum = 0m, bool eligible = true) => new(x.Id, x.CurrencyCode, x.AvailableBalance, x.HeldBalance, x.Status.ToString(), minimum, eligible); private static DepositDto Map(MerchantDeposit x, string businessName) => new(x.Id, x.MerchantId, businessName, x.Amount, x.CurrencyCode, x.Status.ToString(), x.ExternalReference, x.ProofMetadata, x.SubmittedAtUtc, x.VerifiedAtUtc, x.FailureReason); private static PurchaseDto Map(PurchaseTransaction p, CommissionCalculationSnapshot s, string creator, string location, decimal? balance) => new(p.Id, p.PublicTransactionId, p.Status.ToString(), p.PurchaseAmount, p.CurrencyCode, s.TotalCommissionAmount, s.CreatorCommissionAmount, s.PlatformCommissionAmount, creator, location, p.ConfirmedAtUtc, balance, "Purchase confirmed.");
}
public sealed class IdempotencyConflictException : Exception { public IdempotencyConflictException() : base("The idempotency key was already used with a different request.") { } }
public sealed class PurchaseRejectedException(string code, string message) : Exception(message) { public string Code { get; } = code; }
