using Dapper;
using SalesDistricts.Data.Migrations;

namespace SalesDistricts.Tests.Database;

[Collection(SqlServerCollection.Name)]
public sealed class MigratorTests(SqlServerFixture fixture)
{
    private static Migration Fake(long version, string table) =>
        new(version, table, $"CREATE TABLE dbo.{table} (Id int NOT NULL PRIMARY KEY);", $"DROP TABLE dbo.{table};");

    private Migrator NewMigrator(params Migration[] migrations) =>
        new(fixture.ConnectionStringFor($"MigratorTest_{Guid.NewGuid():N}"), migrations);

    [Fact]
    public async Task Up_applies_pending_migrations_in_version_order_and_journals_them()
    {
        var migrator = NewMigrator(Fake(2, "B"), Fake(1, "A"));

        var applied = await migrator.UpAsync();
        var status = await migrator.StatusAsync();

        Assert.Equal(2, applied);
        Assert.Equal([1L, 2L], status.Applied.Select(a => a.Version));
        Assert.Empty(status.Pending);
    }

    [Fact]
    public async Task Up_is_idempotent_and_only_applies_new_migrations()
    {
        var first = NewMigrator(Fake(1, "A"));
        await first.UpAsync();
        Assert.Equal(0, await first.UpAsync());

        var status = await first.StatusAsync();
        Assert.Single(status.Applied);
        Assert.Empty(status.Pending);
    }

    [Fact]
    public async Task Status_reports_applied_and_pending()
    {
        var cs = fixture.ConnectionStringFor($"MigratorTest_{Guid.NewGuid():N}");
        await new Migrator(cs, [Fake(1, "A")]).UpAsync();

        var status = await new Migrator(cs, [Fake(1, "A"), Fake(2, "B"), Fake(3, "C")]).StatusAsync();

        Assert.Equal([1L], status.Applied.Select(a => a.Version));
        Assert.Equal([2L, 3L], status.Pending.Select(p => p.Version));
    }

    [Fact]
    public async Task Down_reverts_newest_first_to_the_target_version()
    {
        var migrator = NewMigrator(Fake(1, "A"), Fake(2, "B"), Fake(3, "C"));
        await migrator.UpAsync();

        var reverted = await migrator.DownAsync(targetVersion: 1);
        var status = await migrator.StatusAsync();

        Assert.Equal(2, reverted);
        Assert.Equal([1L], status.Applied.Select(a => a.Version));
        Assert.Equal([2L, 3L], status.Pending.Select(p => p.Version));
    }

    [Fact]
    public async Task Down_to_zero_reverts_everything_and_up_reapplies()
    {
        var migrator = NewMigrator(Fake(1, "A"), Fake(2, "B"));
        await migrator.UpAsync();

        Assert.Equal(2, await migrator.DownAsync());
        Assert.Empty((await migrator.StatusAsync()).Applied);
        Assert.Equal(2, await migrator.UpAsync());
    }

    [Fact]
    public async Task A_failing_migration_is_rolled_back_and_not_journaled()
    {
        var cs = fixture.ConnectionStringFor($"MigratorTest_{Guid.NewGuid():N}");
        var broken = new Migration(2, "Broken", "CREATE TABLE dbo.HalfDone (Id int NOT NULL PRIMARY KEY);\nGO\nSELECT 1/0;", "DROP TABLE dbo.HalfDone;");
        var migrator = new Migrator(cs, [Fake(1, "A"), broken]);

        await Assert.ThrowsAnyAsync<Exception>(() => migrator.UpAsync());

        var status = await migrator.StatusAsync();
        Assert.Equal([1L], status.Applied.Select(a => a.Version));
        using var c = SqlServerFixture.Open(cs);
        Assert.Null(await c.ExecuteScalarAsync<int?>("SELECT OBJECT_ID('dbo.HalfDone')"));
    }

    [Fact]
    public async Task Real_schema_round_trips_through_up_and_down()
    {
        var cs = fixture.ConnectionStringFor($"MigratorTest_{Guid.NewGuid():N}");
        var migrator = new Migrator(cs);

        await migrator.UpAsync();
        using var c = SqlServerFixture.Open(cs);
        Assert.Equal(4, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('sales')"));
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.views WHERE schema_id = SCHEMA_ID('sales')"));

        await migrator.DownAsync();
        Assert.Null(await c.ExecuteScalarAsync<int?>("SELECT SCHEMA_ID('sales')"));
        Assert.Equal([0L], (await migrator.StatusAsync()).Applied.Select(a => a.Version)); // only the journal itself remains
    }
}
