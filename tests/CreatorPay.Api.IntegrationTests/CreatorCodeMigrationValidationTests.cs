using CreatorPay.Infrastructure.Persistence;
using CreatorPay.Infrastructure.Creators;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class CreatorCodeMigrationValidationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("creator_code_migration_tests").WithUsername("creatorpay").WithPassword("test-only-password").Build();

    public Task InitializeAsync() => container.StartAsync();
    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [DockerFact]
    public async Task Existing_creators_are_backfilled_uniquely_and_down_is_structurally_valid()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260810080924_AddPhoneFirstAuthentication");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO creators ("Id","PublicCreatorId","FirstName","LastName","DisplayName","PhoneNumber","NormalizedPhoneNumber","Email","PreferredLanguage","City","Biography","ContentCategories","TermsAcceptedAtUtc","Status","CreatedAtUtc") VALUES
            ('30000000-0000-0000-0000-000000000091','CR-BACKFILL-0001','Test','One','Test One','+251911000091','+251911000091','','en','Addis Ababa','Test','Testing',CURRENT_TIMESTAMP,'Active',CURRENT_TIMESTAMP),
            ('30000000-0000-0000-0000-000000000092','CR-BACKFILL-0002','Test','Two','Test Two','+251911000092','+251911000092','','en','Addis Ababa','Test','Testing',CURRENT_TIMESTAMP,'Active',CURRENT_TIMESTAMP);
            """);

        await migrator.MigrateAsync("20260811014818_AddPermanentCreatorCode");
        var codes = await db.Creators.AsNoTracking().OrderBy(x => x.Id).Select(x => x.CreatorCode).ToListAsync();
        Assert.Equal(2, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[1-9][0-9]{3}$", code));
        await using (var allocation = await db.Database.BeginTransactionAsync())
        {
            var generated = await new CreatorStore(db).AllocateCreatorCodeAsync(CancellationToken.None);
            Assert.Matches("^[1-9][0-9]{3}$", generated);
            Assert.DoesNotContain(generated, codes);
            await allocation.RollbackAsync();
        }
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE creators SET \"CreatorCode\" = {codes[0]} WHERE \"Id\" = {Guid.Parse("30000000-0000-0000-0000-000000000092")}"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);

        await migrator.MigrateAsync("20260810080924_AddPhoneFirstAuthentication");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='creators' AND column_name='CreatorCode'";
        await db.Database.OpenConnectionAsync();
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }
}
