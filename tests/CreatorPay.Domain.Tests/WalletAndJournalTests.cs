using CreatorPay.Domain.Entities;
using CreatorPay.Domain.Enums;

namespace CreatorPay.Domain.Tests;

public sealed class WalletAndJournalTests
{
    [Fact] public void Debit_never_allows_negative_balance() { var w = new MerchantWallet { Id = Guid.NewGuid(), MerchantId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow }; w.Credit(100, 50, DateTime.UtcNow); Assert.Throws<InvalidOperationException>(() => w.Debit(101, 50, DateTime.UtcNow)); Assert.Equal(100, w.AvailableBalance); }
    [Fact] public void Wallet_tracks_entry_balances_and_low_balance() { var w = new MerchantWallet { Id = Guid.NewGuid(), MerchantId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow }; w.Credit(2000, 1000, DateTime.UtcNow); var b = w.Debit(1500, 1000, DateTime.UtcNow); Assert.Equal((2000m, 500m), b); Assert.Equal(MerchantWalletStatus.LowBalance, w.Status); }
    [Fact] public void Journal_requires_balanced_lines() { var j = new FinancialJournal { Id = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow }; j.Lines.Add(new() { Id = Guid.NewGuid(), Type = JournalLineType.Debit, Amount = 10, CreatedAtUtc = DateTime.UtcNow }); j.Lines.Add(new() { Id = Guid.NewGuid(), Type = JournalLineType.Credit, Amount = 9, CreatedAtUtc = DateTime.UtcNow }); Assert.Throws<InvalidOperationException>(() => j.Post(DateTime.UtcNow)); }
    [Fact] public void Balanced_journal_posts_once_and_is_immutable_by_state() { var j = new FinancialJournal { Id = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow }; j.Lines.Add(new() { Id = Guid.NewGuid(), Type = JournalLineType.Debit, Amount = 10, CreatedAtUtc = DateTime.UtcNow }); j.Lines.Add(new() { Id = Guid.NewGuid(), Type = JournalLineType.Credit, Amount = 10, CreatedAtUtc = DateTime.UtcNow }); j.Post(DateTime.UtcNow); Assert.True(j.IsPosted); Assert.Throws<InvalidOperationException>(() => j.Post(DateTime.UtcNow)); }
}
