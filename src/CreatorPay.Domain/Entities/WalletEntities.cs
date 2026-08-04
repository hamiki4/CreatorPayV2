using CreatorPay.Domain.Common;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Entities;

public sealed class MerchantWallet : Entity
{
    public Guid MerchantId { get; set; }
    public string CurrencyCode { get; set; } = "ETB";
    public decimal AvailableBalance { get; private set; }
    public decimal HeldBalance { get; private set; }
    public MerchantWalletStatus Status { get; private set; } = MerchantWalletStatus.Active;
    public uint ConcurrencyToken { get; set; }
    public Merchant Merchant { get; set; } = null!;
    public ICollection<MerchantWalletEntry> Entries { get; } = [];

    public (decimal Before, decimal After) Credit(decimal amount, decimal lowBalanceThreshold, DateTime now)
    { Validate(amount); var before = AvailableBalance; AvailableBalance += amount; RefreshStatus(lowBalanceThreshold); UpdatedAtUtc = now; return (before, AvailableBalance); }
    public (decimal Before, decimal After) Debit(decimal amount, decimal lowBalanceThreshold, DateTime now)
    { Validate(amount); if (AvailableBalance < amount) throw new InvalidOperationException("The merchant wallet has insufficient available balance."); var before = AvailableBalance; AvailableBalance -= amount; RefreshStatus(lowBalanceThreshold); UpdatedAtUtc = now; return (before, AvailableBalance); }
    private void RefreshStatus(decimal threshold) { if (Status is MerchantWalletStatus.Suspended or MerchantWalletStatus.Closed) return; Status = AvailableBalance < threshold ? MerchantWalletStatus.LowBalance : MerchantWalletStatus.Active; }
    private static void Validate(decimal amount) { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); }
}

public sealed class MerchantWalletEntry : Entity { public Guid MerchantWalletId { get; set; } public Guid MerchantId { get; set; } public MerchantWalletEntryType EntryType { get; set; } public decimal Amount { get; set; } public string CurrencyCode { get; set; } = "ETB"; public decimal BalanceBefore { get; set; } public decimal BalanceAfter { get; set; } public Guid? RelatedTransactionId { get; set; } public Guid? RelatedDepositId { get; set; } public string? IdempotencyKey { get; set; } public string Description { get; set; } = ""; public Guid CreatedByUserId { get; set; } public string CorrelationId { get; set; } = ""; public MerchantWallet Wallet { get; set; } = null!; }
public sealed class MerchantDeposit : Entity { public Guid MerchantId { get; set; } public Guid MerchantWalletId { get; set; } public decimal Amount { get; set; } public string CurrencyCode { get; set; } = "ETB"; public MerchantDepositStatus Status { get; set; } public string ExternalReference { get; set; } = ""; public string? ProofMetadata { get; set; } public DateTime SubmittedAtUtc { get; set; } public DateTime? VerifiedAtUtc { get; set; } public Guid? VerifiedByUserId { get; set; } public string? FailureReason { get; set; } public string IdempotencyKey { get; set; } = ""; public string RequestHash { get; set; } = ""; public MerchantWallet Wallet { get; set; } = null!; }
public sealed class MerchantWalletHold : Entity { public Guid MerchantWalletId { get; set; } public decimal Amount { get; set; } public string CurrencyCode { get; set; } = "ETB"; public string Reason { get; set; } = ""; public Guid? RelatedTransactionId { get; set; } public DateTime? ReleasedAtUtc { get; set; } public Guid? ReleasedByUserId { get; set; } public MerchantWalletHoldStatus Status { get; set; } }
public sealed class FinancialJournal : Entity
{
    public string Reference { get; set; } = ""; public string Description { get; set; } = ""; public DateTime PostedAtUtc { get; private set; }
    public bool IsPosted { get; private set; }
    public Guid? RelatedTransactionId { get; set; }
    public Guid? RelatedDepositId { get; set; }
    public Guid? RelatedPayoutId { get; set; }
    public ICollection<FinancialJournalLine> Lines { get; } = [];
    public void Post(DateTime now) { if (IsPosted) throw new InvalidOperationException("Journal is already posted."); if (Lines.Count < 2 || Lines.Sum(x => x.Type == JournalLineType.Debit ? x.Amount : 0) != Lines.Sum(x => x.Type == JournalLineType.Credit ? x.Amount : 0)) throw new InvalidOperationException("Journal debits and credits must balance."); IsPosted = true; PostedAtUtc = now; }
}
public sealed class FinancialJournalLine : Entity { public Guid FinancialJournalId { get; set; } public JournalAccount Account { get; set; } public JournalLineType Type { get; set; } public decimal Amount { get; set; } public string CurrencyCode { get; set; } = "ETB"; public string Description { get; set; } = ""; public FinancialJournal Journal { get; set; } = null!; }
public sealed class IdempotencyRecord : Entity { public string Scope { get; set; } = ""; public string Key { get; set; } = ""; public string RequestHash { get; set; } = ""; public Guid? ResponseReferenceId { get; set; } public IdempotencyStatus Status { get; set; } public DateTime ExpiresAtUtc { get; set; } }
