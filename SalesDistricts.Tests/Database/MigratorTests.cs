using Dapper;
using SalesDistricts.Data.Migrations;
using Shouldly;

namespace SalesDistricts.Tests.Database;

[Collection(SqlServerCollection.Name)]
public sealed class MigratorTests(SqlServerFixture fixture)
{
    private static Migration Fake(long version, string table) =>
        new(version, table, $"CREATE TABLE dbo.{table} (Id int NOT NULL PRIMARY KEY);", $"DROP TABLE dbo.{table};");

    private string FreshDatabase() => fixture.ConnectionStringFor($"MigratorTest_{Guid.NewGuid():N}");

    private Migrator NewMigrator(params Migration[] migrations) => new(FreshDatabase(), migrations);

    [Fact]
    public async Task Up_applies_pending_migrations_in_version_order_and_journals_them()
    {
        var migrator = NewMigrator(Fake(2, "B"), Fake(1, "A"));

        var applied = await migrator.UpAsync();
        var status = await migrator.StatusAsync();

        applied.ShouldBe(2);
        status.Applied.Select(a => a.Version).ShouldBe([1L, 2L]);
        status.Pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task Up_is_idempotent_and_only_applies_new_migrations()
    {
        var migrator = NewMigrator(Fake(1, "A"));
        await migrator.UpAsync();

        (await migrator.UpAsync()).ShouldBe(0);

        var status = await migrator.StatusAsync();
        status.Applied.ShouldHaveSingleItem();
        status.Pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task Status_reports_applied_and_pending()
    {
        var cs = FreshDatabase();
        await new Migrator(cs, [Fake(1, "A")]).UpAsync();

        var status = await new Migrator(cs, [Fake(1, "A"), Fake(2, "B"), Fake(3, "C")]).StatusAsync();

        status.Applied.Select(a => a.Version).ShouldBe([1L]);
        status.Pending.Select(p => p.Version).ShouldBe([2L, 3L]);
    }

    [Fact]
    public async Task Down_reverts_newest_first_to_the_target_version()
    {
        var migrator = NewMigrator(Fake(1, "A"), Fake(2, "B"), Fake(3, "C"));
        await migrator.UpAsync();

        var reverted = await migrator.DownAsync(targetVersion: 1);
        var status = await migrator.StatusAsync();

        reverted.ShouldBe(2);
        status.Applied.Select(a => a.Version).ShouldBe([1L]);
        status.Pending.Select(p => p.Version).ShouldBe([2L, 3L]);
    }

    [Fact]
    public async Task Down_to_zero_reverts_everything_and_up_reapplies()
    {
        var migrator = NewMigrator(Fake(1, "A"), Fake(2, "B"));
        await migrator.UpAsync();

        (await migrator.DownAsync()).ShouldBe(2);
        (await migrator.StatusAsync()).Applied.ShouldBeEmpty();
        (await migrator.UpAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task A_failing_migration_is_rolled_back_and_not_journaled()
    {
        var cs = FreshDatabase();
        var broken = new Migration(2, "Broken", "CREATE TABLE dbo.HalfDone (Id int NOT NULL PRIMARY KEY);\nGO\nSELECT 1/0;", "DROP TABLE dbo.HalfDone;");
        var migrator = new Migrator(cs, [Fake(1, "A"), broken]);

        await Should.ThrowAsync<Exception>(() => migrator.UpAsync());

        (await migrator.StatusAsync()).Applied.Select(a => a.Version).ShouldBe([1L]);
        using var c = SqlServerFixture.Open(cs);
        (await c.ExecuteScalarAsync<int?>("SELECT OBJECT_ID('dbo.HalfDone')")).ShouldBeNull();
    }

    [Fact]
    public async Task Real_schema_round_trips_through_up_and_down()
    {
        var cs = FreshDatabase();
        var migrator = new Migrator(cs);

        await migrator.UpAsync();
        using var c = SqlServerFixture.Open(cs);
        (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('sales')")).ShouldBe(4);
        (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.views WHERE schema_id = SCHEMA_ID('sales')")).ShouldBe(1);

        await migrator.DownAsync();
        (await c.ExecuteScalarAsync<int?>("SELECT SCHEMA_ID('sales')")).ShouldBeNull();
        (await migrator.StatusAsync()).Applied.Select(a => a.Version).ShouldBe([0L]); // only the journal itself remains
    }
}
