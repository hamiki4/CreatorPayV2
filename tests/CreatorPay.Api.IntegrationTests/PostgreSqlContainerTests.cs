using CreatorPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CreatorPay.Api.IntegrationTests;

public sealed class PostgreSqlContainerTests : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    private string? unavailableReason;
    public async Task InitializeAsync() { if (OperatingSystem.IsWindows() && !File.Exists(@"\\.\pipe\docker_engine")) { unavailableReason = "Docker is unavailable: the docker_engine named pipe does not exist."; return; } try { container = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("creatorpay_tests").WithUsername("creatorpay").WithPassword("test-only-password").Build(); await container.StartAsync(); } catch (Exception ex) { unavailableReason = $"Docker is unavailable: {ex.Message}"; } }
    public async Task DisposeAsync() { if (container is not null && unavailableReason is null) await container.DisposeAsync(); }
    private void RequireDocker() { if (unavailableReason is not null) throw new InvalidOperationException(unavailableReason); }
    private ApplicationDbContext Db() { var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(container!.GetConnectionString()).Options; return new ApplicationDbContext(options); }

    [DockerFact]
    public async Task AllMigrationsApplyToPostgreSql()
    {
        RequireDocker(); await using var db = Db(); await db.Database.MigrateAsync(); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [DockerFact]
    public async Task PostgreSqlAdvisoryLockPreventsOverlappingJobExecution()
    {
        RequireDocker(); await using var first = new NpgsqlConnection(container!.GetConnectionString()); await using var second = new NpgsqlConnection(container.GetConnectionString()); await first.OpenAsync(); await second.OpenAsync();
        await using var acquire1 = new NpgsqlCommand("SELECT pg_try_advisory_lock(15015)", first); await using var acquire2 = new NpgsqlCommand("SELECT pg_try_advisory_lock(15015)", second);
        Assert.True((bool)(await acquire1.ExecuteScalarAsync())!); Assert.False((bool)(await acquire2.ExecuteScalarAsync())!);
    }

    [DockerFact]
    public async Task OperationalSchemaContainsConcurrencyConstraints()
    {
        RequireDocker(); await using var db = Db(); await db.Database.MigrateAsync();
        var migrationNames = await db.Database.GetAppliedMigrationsAsync(); Assert.Contains(migrationNames, x => x.EndsWith("AddOperationalReadiness", StringComparison.Ordinal));
        Assert.Contains(db.Model.FindEntityType("CreatorPay.Domain.Entities.CreatorQrCode")!.GetIndexes(), x => x.IsUnique);
        Assert.Contains(db.Model.FindEntityType("CreatorPay.Domain.Entities.IdempotencyRecord")!.GetIndexes(), x => x.IsUnique);
        Assert.Contains(db.Model.FindEntityType("CreatorPay.Domain.Entities.PayoutItem")!.GetIndexes(), x => x.IsUnique);
    }
}

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute() { if (OperatingSystem.IsWindows() && !File.Exists(@"\\.\pipe\docker_engine")) Skip = "Docker is unavailable: the docker_engine named pipe does not exist."; }
}
