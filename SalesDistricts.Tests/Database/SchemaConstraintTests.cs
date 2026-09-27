using Dapper;
using Microsoft.Data.SqlClient;

namespace SalesDistricts.Tests.Database;

/// <summary>
/// Task 1: "As much as possible the business rules should be enforced by constraints in the database."
/// Each test drives the schema with SQL and asserts on the error number SQL Server returns.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SchemaConstraintTests(SqlServerFixture fixture)
{
    private const int CannotInsertNull = 515;
    private const int ConstraintViolation = 547; // FK and CHECK
    private const int DuplicateKey = 2627;       // PK and UNIQUE constraints
    private const int DuplicateIndexKey = 2601;  // unique indexes

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static Task<Guid> InsertSalesperson(SqlConnection c, string? name = null, string? email = null) =>
        c.QuerySingleAsync<Guid>("INSERT INTO sales.Salesperson (Name, Email) OUTPUT inserted.Id VALUES (@name, @email)",
            new { name = name ?? Unique("Salesperson"), email });

    private static Task<Guid> InsertDistrict(SqlConnection c, Guid primaryId, string? name = null) =>
        c.QuerySingleAsync<Guid>("INSERT INTO sales.District (Name, PrimarySalespersonId) OUTPUT inserted.Id VALUES (@name, @primaryId)",
            new { name = name ?? Unique("District"), primaryId });

    private static Task<Guid> InsertStore(SqlConnection c, Guid districtId, string? name = null) =>
        c.QuerySingleAsync<Guid>("INSERT INTO sales.Store (Name, DistrictId) OUTPUT inserted.Id VALUES (@name, @districtId)",
            new { name = name ?? Unique("Store"), districtId });

    private static Task<int> AddSecondary(SqlConnection c, Guid districtId, Guid salespersonId) =>
        c.ExecuteAsync("INSERT INTO sales.DistrictSecondarySalesperson (DistrictId, SalespersonId) VALUES (@districtId, @salespersonId)",
            new { districtId, salespersonId });

    private static async Task AssertSqlError(int expectedNumber, Func<Task> action)
    {
        var ex = await Assert.ThrowsAsync<SqlException>(action);
        Assert.Equal(expectedNumber, ex.Number);
    }

    [Fact]
    public async Task Keys_are_generated_by_the_database()
    {
        using var c = fixture.Open();
        var first = await InsertSalesperson(c);
        var second = await InsertSalesperson(c);

        Assert.NotEqual(Guid.Empty, first);
        Assert.NotEqual(first, second);
    }

    // ---- exactly one primary per district ---------------------------------------------------

    [Fact]
    public async Task District_cannot_be_created_without_a_primary_salesperson()
    {
        using var c = fixture.Open();
        await AssertSqlError(CannotInsertNull, () =>
            c.ExecuteAsync("INSERT INTO sales.District (Name, PrimarySalespersonId) VALUES (@name, NULL)", new { name = Unique("District") }));
    }

    [Fact]
    public async Task District_primary_must_be_an_existing_salesperson()
    {
        using var c = fixture.Open();
        await AssertSqlError(ConstraintViolation, () => InsertDistrict(c, Guid.NewGuid()));
    }

    [Fact]
    public async Task District_primary_cannot_be_cleared()
    {
        using var c = fixture.Open();
        var district = await InsertDistrict(c, await InsertSalesperson(c));
        await AssertSqlError(CannotInsertNull, () =>
            c.ExecuteAsync("UPDATE sales.District SET PrimarySalespersonId = NULL WHERE Id = @district", new { district }));
    }

    [Fact]
    public async Task Primary_salesperson_cannot_be_deleted_while_assigned_to_a_district()
    {
        using var c = fixture.Open();
        var primary = await InsertSalesperson(c);
        await InsertDistrict(c, primary);
        await AssertSqlError(ConstraintViolation, () =>
            c.ExecuteAsync("DELETE FROM sales.Salesperson WHERE Id = @primary", new { primary }));
    }

    [Fact]
    public async Task Same_salesperson_can_be_primary_for_multiple_districts()
    {
        using var c = fixture.Open();
        var primary = await InsertSalesperson(c);
        var first = await InsertDistrict(c, primary);
        var second = await InsertDistrict(c, primary);

        Assert.Equal(2, await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.District WHERE PrimarySalespersonId = @primary AND Id IN (@first, @second)", new { primary, first, second }));
    }

    // ---- zero or more secondaries -----------------------------------------------------------

    [Fact]
    public async Task District_can_have_several_secondary_salespersons()
    {
        using var c = fixture.Open();
        var district = await InsertDistrict(c, await InsertSalesperson(c));
        await AddSecondary(c, district, await InsertSalesperson(c));
        await AddSecondary(c, district, await InsertSalesperson(c));

        Assert.Equal(2, await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sales.DistrictSecondarySalesperson WHERE DistrictId = @district", new { district }));
    }

    [Fact]
    public async Task Secondary_salesperson_cannot_be_listed_twice_for_the_same_district()
    {
        using var c = fixture.Open();
        var district = await InsertDistrict(c, await InsertSalesperson(c));
        var secondary = await InsertSalesperson(c);
        await AddSecondary(c, district, secondary);
        await AssertSqlError(DuplicateKey, () => AddSecondary(c, district, secondary));
    }

    [Fact]
    public async Task Secondary_assignment_requires_existing_district_and_salesperson()
    {
        using var c = fixture.Open();
        var district = await InsertDistrict(c, await InsertSalesperson(c));
        var salesperson = await InsertSalesperson(c);
        await AssertSqlError(ConstraintViolation, () => AddSecondary(c, Guid.NewGuid(), salesperson));
        await AssertSqlError(ConstraintViolation, () => AddSecondary(c, district, Guid.NewGuid()));
    }

    [Fact]
    public async Task Deleting_a_district_removes_its_secondary_assignments()
    {
        using var c = fixture.Open();
        var district = await InsertDistrict(c, await InsertSalesperson(c));
        var secondary = await InsertSalesperson(c);
        await AddSecondary(c, district, secondary);

        await c.ExecuteAsync("DELETE FROM sales.District WHERE Id = @district", new { district });

        Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sales.DistrictSecondarySalesperson WHERE DistrictId = @district", new { district }));
        Assert.Equal(1, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sales.Salesperson WHERE Id = @secondary", new { secondary }));
    }

    [Fact]
    public async Task Deleting_a_secondary_only_salesperson_removes_their_assignments()
    {
        using var c = fixture.Open();
        var district = await InsertDistrict(c, await InsertSalesperson(c));
        var secondary = await InsertSalesperson(c);
        await AddSecondary(c, district, secondary);

        await c.ExecuteAsync("DELETE FROM sales.Salesperson WHERE Id = @secondary", new { secondary });

        Assert.Equal(0, await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sales.DistrictSecondarySalesperson WHERE SalespersonId = @secondary", new { secondary }));
    }

    // ---- stores -----------------------------------------------------------------------------

    [Fact]
    public async Task Store_must_belong_to_an_existing_district()
    {
        using var c = fixture.Open();
        await AssertSqlError(ConstraintViolation, () => InsertStore(c, Guid.NewGuid()));
        await AssertSqlError(CannotInsertNull, () =>
            c.ExecuteAsync("INSERT INTO sales.Store (Name, DistrictId) VALUES (@name, NULL)", new { name = Unique("Store") }));
    }

    [Fact]
    public async Task Store_names_are_unique_within_a_district_but_may_repeat_across_districts()
    {
        using var c = fixture.Open();
        var primary = await InsertSalesperson(c);
        var first = await InsertDistrict(c, primary);
        var second = await InsertDistrict(c, primary);
        var name = Unique("Main Street");

        await InsertStore(c, first, name);
        await InsertStore(c, second, name);
        await AssertSqlError(DuplicateKey, () => InsertStore(c, first, name));
    }

    [Fact]
    public async Task District_with_stores_cannot_be_deleted()
    {
        using var c = fixture.Open();
        var district = await InsertDistrict(c, await InsertSalesperson(c));
        await InsertStore(c, district);
        await AssertSqlError(ConstraintViolation, () =>
            c.ExecuteAsync("DELETE FROM sales.District WHERE Id = @district", new { district }));
    }

    // ---- names and emails -------------------------------------------------------------------

    [Fact]
    public async Task District_names_are_unique()
    {
        using var c = fixture.Open();
        var primary = await InsertSalesperson(c);
        var name = Unique("North Denmark");
        await InsertDistrict(c, primary, name);
        await AssertSqlError(DuplicateKey, () => InsertDistrict(c, primary, name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_names_are_rejected(string blank)
    {
        using var c = fixture.Open();
        var primary = await InsertSalesperson(c);
        var district = await InsertDistrict(c, primary);

        await AssertSqlError(ConstraintViolation, () => InsertSalesperson(c, blank));
        await AssertSqlError(ConstraintViolation, () => InsertDistrict(c, primary, blank));
        await AssertSqlError(ConstraintViolation, () => InsertStore(c, district, blank));
    }

    [Fact]
    public async Task Salesperson_email_is_optional_but_unique_when_present()
    {
        using var c = fixture.Open();
        var email = $"{Guid.NewGuid():N}@example.com";

        await InsertSalesperson(c, email: null);
        await InsertSalesperson(c, email: null);
        await InsertSalesperson(c, email: email);
        await AssertSqlError(DuplicateIndexKey, () => InsertSalesperson(c, email: email));
    }

    // ---- view -------------------------------------------------------------------------------

    [Fact]
    public async Task View_lists_primary_and_secondaries_with_role_flag()
    {
        using var c = fixture.Open();
        var primary = await InsertSalesperson(c);
        var secondaryA = await InsertSalesperson(c);
        var secondaryB = await InsertSalesperson(c);
        var district = await InsertDistrict(c, primary);
        await AddSecondary(c, district, secondaryA);
        await AddSecondary(c, district, secondaryB);

        var rows = (await c.QueryAsync<(Guid SalespersonId, bool IsPrimary)>(
            "SELECT SalespersonId, IsPrimary FROM sales.vDistrictSalesperson WHERE DistrictId = @district", new { district })).ToList();

        Assert.Equal(3, rows.Count);
        Assert.Single(rows, r => r.IsPrimary && r.SalespersonId == primary);
        Assert.Contains(rows, r => r.SalespersonId == secondaryA && !r.IsPrimary);
        Assert.Contains(rows, r => r.SalespersonId == secondaryB && !r.IsPrimary);
    }
}
