using CreatorPay.Domain.Entities;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CreatorPay.Infrastructure.Tests;

public sealed class ApplicationDbContextModelTests
{
    [Fact]
    public void Wallet_and_purchase_constraints_are_in_model()
    {
        using var db = CreateContext();
        var wallet = db.Model.FindEntityType(typeof(CreatorPay.Domain.Entities.MerchantWallet))!;
        Assert.Contains(wallet.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["MerchantId", "CurrencyCode"]));
        var purchase = db.Model.FindEntityType(typeof(CreatorPay.Domain.Entities.PurchaseTransaction))!;
        Assert.Contains(purchase.GetIndexes(), x => x.IsUnique && x.Properties.Any(p => p.Name == "PublicTransactionId"));
    }

    [Fact]
    public void Payout_schedules_are_effective_dated_and_versioned()
    {
        var schedule = _model.FindEntityType(typeof(PayoutScheduleVersion));
        Assert.NotNull(schedule);
        Assert.Contains(schedule!.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["CurrencyCode", "VersionNumber"]));
        Assert.Contains(schedule.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["CurrencyCode", "EffectiveFromUtc"]));
    }

    [Fact]
    public void Operational_alerts_have_history_and_active_cooldown_deduplication()
    {
        var alert = _model.FindEntityType(typeof(OperationalAlert))!;
        Assert.NotNull(_model.FindEntityType(typeof(OperationalAlertHistory)));
        var cooldown = alert.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(["CooldownKey", "Status"]));
        Assert.True(cooldown.IsUnique);
        Assert.Contains("Resolved", cooldown.GetFilter());
    }
    [Fact]
    public void Reporting_governance_entities_have_owner_and_version_constraints()
    {
        Assert.NotNull(_model.FindEntityType(typeof(ReportExportAudit)));
        Assert.Contains(_model.FindEntityType(typeof(SavedReportView))!.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["OwnerUserId", "ReportType", "Name"]));
        Assert.Contains(_model.FindEntityType(typeof(AlertThresholdPolicy))!.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["AlertType", "Version"]));
    }
    private readonly IModel _model = CreateContext().Model;

    [Fact]
    public void Model_CreatesAllMilestoneTwoEntities()
    {
        Type[] expected = [typeof(UserAccount), typeof(Creator), typeof(CreatorQrCode), typeof(Merchant), typeof(MerchantLocation), typeof(Supervisor), typeof(Cashier), typeof(CashierLocationAssignment), typeof(SupervisorLocationAssignment), typeof(MerchantCreatorPartnership), typeof(PartnershipLocation)];
        Assert.All(expected, type => Assert.NotNull(_model.FindEntityType(type)));
    }

    [Fact]
    public void Model_CreatesAuthenticationEntitiesAndUniqueTokenIndexes()
    {
        Type[] expected = [typeof(RefreshToken), typeof(LoginAudit), typeof(PasswordResetToken)];
        Assert.All(expected, type => Assert.NotNull(_model.FindEntityType(type)));
        Assert.True(FindIndex(typeof(RefreshToken), "TokenHash").IsUnique);
        Assert.True(FindIndex(typeof(PasswordResetToken), "TokenHash").IsUnique);
        Assert.False(FindIndex(typeof(RefreshToken), "UserAccountId", "ExpiresAtUtc").IsUnique);
    }

    [Fact]
    public void Model_CreatesCreatorOnboardingAuditAndHashedTokenStorage()
    {
        Assert.NotNull(_model.FindEntityType(typeof(CreatorAuditEvent)));
        Assert.NotNull(_model.FindEntityType(typeof(CreatorVerificationToken)));
        Assert.True(FindIndex(typeof(CreatorVerificationToken), "TokenHash").IsUnique);
        Assert.Null(_model.FindEntityType(typeof(CreatorVerificationToken))!.FindProperty("Token"));
    }

    [Fact]
    public void Model_ConfiguresStaffInvitationAndPrimaryLocationConstraints()
    {
        Assert.NotNull(_model.FindEntityType(typeof(StaffInvitation)));
        Assert.True(FindIndex(typeof(StaffInvitation), "TokenHash").IsUnique);
        Assert.Null(_model.FindEntityType(typeof(StaffInvitation))!.FindProperty("Token"));
        var primary = FindIndex(typeof(CashierLocationAssignment), "CashierId");
        Assert.True(primary.IsUnique);
        Assert.Contains("IsPrimary", primary.GetFilter());
        Assert.Contains("IsActive", primary.GetFilter());
    }

    [Theory]
    [InlineData(typeof(UserAccount), "NormalizedEmail")]
    [InlineData(typeof(Creator), "PublicCreatorId")]
    [InlineData(typeof(Creator), "CreatorCode")]
    [InlineData(typeof(Creator), "NormalizedPhoneNumber")]
    [InlineData(typeof(Merchant), "PublicMerchantId")]
    public void Model_HasRequiredUniqueSingleColumnIndexes(Type entityType, string property)
    {
        var index = _model.FindEntityType(entityType)!.GetIndexes().SingleOrDefault(x => x.Properties.Select(p => p.Name).SequenceEqual([property]));
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Theory]
    [InlineData(typeof(MerchantCreatorPartnership), "MerchantId", "CreatorId")]
    [InlineData(typeof(CashierLocationAssignment), "CashierId", "MerchantLocationId")]
    [InlineData(typeof(SupervisorLocationAssignment), "SupervisorId", "MerchantLocationId")]
    [InlineData(typeof(PartnershipLocation), "MerchantCreatorPartnershipId", "MerchantLocationId")]
    public void Model_HasRequiredUniqueCompositeIndexes(Type entityType, string first, string second)
    {
        var index = _model.FindEntityType(entityType)!.GetIndexes().SingleOrDefault(x => x.Properties.Select(p => p.Name).SequenceEqual([first, second]));
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Theory]
    [InlineData(typeof(MerchantLocation), "MerchantId", typeof(Merchant))]
    [InlineData(typeof(MerchantCreatorPartnership), "MerchantId", typeof(Merchant))]
    [InlineData(typeof(MerchantCreatorPartnership), "CreatorId", typeof(Creator))]
    [InlineData(typeof(CashierLocationAssignment), "CashierId", typeof(Cashier))]
    [InlineData(typeof(CashierLocationAssignment), "MerchantLocationId", typeof(MerchantLocation))]
    [InlineData(typeof(SupervisorLocationAssignment), "SupervisorId", typeof(Supervisor))]
    [InlineData(typeof(SupervisorLocationAssignment), "MerchantLocationId", typeof(MerchantLocation))]
    public void Model_ConfiguresCoreRelationships(Type dependent, string foreignKey, Type principal)
    {
        var relationship = _model.FindEntityType(dependent)!.GetForeignKeys().SingleOrDefault(x => x.Properties.Single().Name == foreignKey && x.PrincipalEntityType.ClrType == principal);
        Assert.NotNull(relationship);
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=CreatorPayV2Db;Username=test;Password=test")
            .Options;
        return new ApplicationDbContext(options);
    }

    private IIndex FindIndex(Type type, params string[] properties) => _model.FindEntityType(type)!.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(properties));
}
