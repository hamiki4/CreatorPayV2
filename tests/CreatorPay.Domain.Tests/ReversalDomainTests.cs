using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Tests;

public sealed class ReversalDomainTests
{
    [Fact] public void Full_reversal_changes_transaction_and_appends_history() { var actor = Guid.NewGuid(); var tx = new PurchaseTransaction { Id = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow }; tx.Confirm(DateTime.UtcNow, actor); tx.ApplyReversal(true, DateTime.UtcNow, actor, "corr"); Assert.Equal(TransactionStatus.Reversed, tx.Status); Assert.Equal(2, tx.StatusHistory.Count); Assert.Equal("Financial reversal posted", tx.StatusHistory.Last().Reason); }
    [Fact] public void Partial_reversal_marks_transaction_partially_reversed() { var actor = Guid.NewGuid(); var tx = new PurchaseTransaction { Id = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow }; tx.Confirm(DateTime.UtcNow, actor); tx.ApplyReversal(false, DateTime.UtcNow, actor, "corr"); Assert.Equal(TransactionStatus.PartiallyReversed, tx.Status); }
    [Fact] public void Posted_journal_for_reversal_must_balance() { var j = new FinancialJournal(); j.Lines.Add(new() { Account = JournalAccount.PlatformCommissionRevenue, Type = JournalLineType.Debit, Amount = 2 }); j.Lines.Add(new() { Account = JournalAccount.CreatorPayable, Type = JournalLineType.Debit, Amount = 8 }); j.Lines.Add(new() { Account = JournalAccount.MerchantWalletLiability, Type = JournalLineType.Credit, Amount = 10 }); j.Post(DateTime.UtcNow); Assert.True(j.IsPosted); }
    [Fact] public void Creator_balance_cannot_silently_go_negative() { var a = new CreatorBalanceAccount(); Assert.Throws<InvalidOperationException>(() => a.ReverseFrom(CreatorBalanceCategory.Available, 1, DateTime.UtcNow)); }
    [Fact] public void Unpaid_earning_can_be_marked_reversed_without_deletion() { var e = new CreatorEarning(); e.Reverse(DateTime.UtcNow); Assert.Equal(CreatorEarningStatus.Reversed, e.Status); Assert.NotNull(e.ReversedAtUtc); }
}
