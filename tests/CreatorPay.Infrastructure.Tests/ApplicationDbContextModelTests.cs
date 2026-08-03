using CreatorPay.Domain.Entities;
using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CreatorPay.Infrastructure.Tests;

public sealed class ApplicationDbContextModelTests
{
    private readonly IModel _model = CreateContext().Model;

    [Fact]
    public void Model_CreatesAllMilestoneTwoEntities()
    {
        Type[] expected = [typeof(UserAccount), typeof(Creator), typeof(Merchant), typeof(MerchantLocation), typeof(Supervisor), typeof(Cashier), typeof(CashierLocationAssignment), typeof(SupervisorLocationAssignment), typeof(MerchantCreatorPartnership), typeof(PartnershipLocation)];
        Assert.All(expected, type => Assert.NotNull(_model.FindEntityType(type)));
    }

    [Theory]
    [InlineData(typeof(UserAccount), "NormalizedEmail")]
    [InlineData(typeof(Creator), "PublicCreatorId")]
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
}
