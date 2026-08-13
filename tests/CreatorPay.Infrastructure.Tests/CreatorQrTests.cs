using CreatorPay.Domain.Entities;
using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Infrastructure.Qr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CreatorPay.Infrastructure.Tests;

public sealed class CreatorQrTests
{
    private static QrTokenService Tokens() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Jwt:SigningKey"] = new string('x', 64) }).Build());

    [Fact]
    public void Token_IsDeterministicTamperEvidentAndNotStoredRaw()
    {
        var service = Tokens(); var token = service.CreateToken("public-qr", 1); var hash = service.Hash(token);
        Assert.True(service.FixedTimeEquals(token, hash)); Assert.False(service.FixedTimeEquals(token + "x", hash)); Assert.DoesNotContain(token, hash);
    }

    [Fact]
    public void ImageGenerator_ReturnsPng()
    {
        var bytes = new QrImageGenerator().GeneratePng("https://creatorpay.example/c/example?t=safe");
        Assert.True(bytes.Length > 100); Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
    }

    [Fact]
    public void UrlBuilder_ChangesOnlyConfiguredOriginAndPreservesPermanentIdentity()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"PublicAppBaseUrl","https://pilot.weymela.com"}}).Build();
        var url=new CreatorQrUrlBuilder(configuration).Create("existing-public-id",1,"signed_token_value_that_is_long_enough_123");
        Assert.Equal("https://pilot.weymela.com/c/existing-public-id?t=signed_token_value_that_is_long_enough_123&v=1",url);
    }

    [Fact]
    public void Revocation_IsImmediateAndRetainsHistoryMetadata()
    {
        var actor = Guid.NewGuid(); var qr = new CreatorQrCode(); qr.Revoke(DateTime.UtcNow, actor, "Regenerated");
        Assert.False(qr.IsActive); Assert.NotNull(qr.RevokedAtUtc); Assert.Equal(actor, qr.RevokedByUserId); Assert.Equal("Regenerated", qr.RevocationReason);
    }

    [Fact]
    public void Model_EnforcesOneActiveQrAndUniquePublicIdentifiers()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=test;Username=test;Password=test").Options);
        var entity = db.Model.FindEntityType(typeof(CreatorQrCode))!; var indexes = entity.GetIndexes().ToList();
        Assert.Contains(indexes, x => x.IsUnique && x.Properties.Single().Name == "PublicQrId");
        Assert.Contains(indexes, x => x.IsUnique && x.Properties.Single().Name == "TokenHash");
        Assert.Contains(indexes, x => x.IsUnique && x.Properties.Single().Name == "CreatorId" && x.GetFilter()!.Contains("IsActive"));
    }
}
