using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class PhoneFirstMigrationValidationTests : IAsyncLifetime
{
    private const string Previous = "20260809055806_AddPlatformFinancialSettings";
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("creatorpay_phone_migration_tests").WithUsername("creatorpay").WithPassword("test-only-password").Build();

    public Task InitializeAsync() => container.StartAsync();
    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    [DockerFact]
    public async Task Phone_first_migration_backfills_enforces_rolls_back_and_aborts_duplicates()
    {
        await using var db = Db();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);

        await SeedCustomer(db, "10000000-0000-0000-0000-000000000001", "20000000-0000-0000-0000-000000000001", "0911 555-555", "0911555555", "legacy-one@example.test");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO user_accounts ("Id","Email","NormalizedEmail","PasswordHash","Role","Status","IsEmailVerified","IsPhoneVerified","CreatedAtUtc")
            VALUES ('20000000-0000-0000-0000-000000000099','','','hash','PlatformAdmin','Active',true,false,now())
            """);

        await migrator.MigrateAsync();
        var canonical = await db.Database.SqlQueryRaw<string>("SELECT \"NormalizedPhoneNumber\" AS \"Value\" FROM user_accounts WHERE \"Id\"='20000000-0000-0000-0000-000000000001'").SingleAsync();
        Assert.Equal("+251911555555", canonical);
        var missing = await db.Database.SqlQueryRaw<string?>("SELECT \"NormalizedPhoneNumber\" AS \"Value\" FROM user_accounts WHERE \"Id\"='20000000-0000-0000-0000-000000000099'").SingleAsync();
        Assert.Null(missing);
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('phone_otp_challenges') IS NOT NULL AS \"Value\"").SingleAsync());
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT is_nullable = 'YES' AS \"Value\" FROM information_schema.columns WHERE table_name = 'merchants' AND column_name = 'Email'").SingleAsync());
        var merchantEmailFilter = await db.Database.SqlQueryRaw<string>("SELECT pg_get_expr(i.indpred, i.indrelid) AS \"Value\" FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE c.relname = 'IX_merchants_Email'").SingleAsync();
        Assert.Contains("IS NOT NULL", merchantEmailFilter, StringComparison.Ordinal);
        Assert.Contains("<> ''", merchantEmailFilter, StringComparison.Ordinal);
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO user_accounts ("Id","Email","NormalizedEmail","PasswordHash","Role","Status","PhoneNumber","NormalizedPhoneNumber","IsEmailVerified","IsPhoneVerified","CreatedAtUtc")
            VALUES ('20000000-0000-0000-0000-000000000002','','','hash','Customer','Active','+251911555555','+251911555555',false,true,now())
            """));

        await migrator.MigrateAsync(Previous);
        Assert.False(await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('phone_otp_challenges') IS NOT NULL AS \"Value\"").SingleAsync());
        Assert.False(await db.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='user_accounts' AND column_name='NormalizedPhoneNumber') AS \"Value\"").SingleAsync());

        await db.Database.ExecuteSqlRawAsync("DELETE FROM user_accounts; DELETE FROM \"Customers\";");
        await SeedCustomer(db, "10000000-0000-0000-0000-000000000011", "20000000-0000-0000-0000-000000000011", "0911666666", "0911666666", "duplicate-one@example.test");
        await SeedCustomer(db, "10000000-0000-0000-0000-000000000012", "20000000-0000-0000-0000-000000000012", "+251911666666", "+251911666666", "duplicate-two@example.test");
        var error = await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync());
        Assert.Contains("duplicate normalized phone identities", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(container.GetConnectionString()).Options);

    private static async Task SeedCustomer(ApplicationDbContext db, string customerId, string userId, string phone, string normalized, string email)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Customers" ("Id","PublicCustomerId","DisplayName","PhoneNumber","NormalizedPhoneNumber","Status","CreatedAtUtc")
            VALUES ({Guid.Parse(customerId)}, {"CU-" + customerId[^4..]}, 'Legacy Shopper', {phone}, {normalized}, 'Active', now());
            INSERT INTO user_accounts ("Id","Email","NormalizedEmail","PasswordHash","Role","Status","CustomerId","IsEmailVerified","IsPhoneVerified","CreatedAtUtc")
            VALUES ({Guid.Parse(userId)}, {email}, {email.ToUpperInvariant()}, 'hash', 'Customer', 'Active', {Guid.Parse(customerId)}, false, true, now());
            """);
    }
}
