using CreatorPay.Domain.Entities;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPay.Infrastructure.Tests;

public sealed class RiskModelTests
{
    static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=model;Username=x;Password=x").Options);
    [Fact] public void Public_risk_ids_and_operation_keys_are_unique() { using var db = Context(); var model = db.Model; Assert.Contains(model.FindEntityType(typeof(FraudAlert))!.GetIndexes(), x => x.IsUnique && x.Properties.Any(p => p.Name == nameof(FraudAlert.PublicFraudAlertId))); Assert.Contains(model.FindEntityType(typeof(Dispute))!.GetIndexes(), x => x.IsUnique && x.Properties.Any(p => p.Name == nameof(Dispute.PublicDisputeId))); Assert.Contains(model.FindEntityType(typeof(TransactionReversal))!.GetIndexes(), x => x.IsUnique && x.Properties.Any(p => p.Name == nameof(TransactionReversal.IdempotencyKey))); }
    [Fact] public void Evidence_metadata_uses_jsonb() { using var db = Context(); Assert.Equal("jsonb", db.Model.FindEntityType(typeof(DisputeEvidence))!.FindProperty(nameof(DisputeEvidence.MetadataJson))!.GetColumnType()); Assert.Equal("jsonb", db.Model.FindEntityType(typeof(FraudAlert))!.FindProperty(nameof(FraudAlert.EvidenceJson))!.GetColumnType()); }
    [Fact] public void Reversal_money_uses_18_2_precision() { using var db = Context(); var p = db.Model.FindEntityType(typeof(TransactionReversal))!.FindProperty(nameof(TransactionReversal.Amount))!; Assert.Equal(18, p.GetPrecision()); Assert.Equal(2, p.GetScale()); }
}
