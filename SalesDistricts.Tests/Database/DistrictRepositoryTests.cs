using SalesDistricts.Data;
using Shouldly;

namespace SalesDistricts.Tests.Database;

/// <summary>Read queries against the seeded schema (0002_SeedData).</summary>
[Collection(SqlServerCollection.Name)]
public sealed class DistrictRepositoryTests(SqlServerFixture fixture)
{
    private static readonly Guid CapitalRegion = Guid.Parse("D0000000-0000-0000-0000-000000000005");
    private static readonly Guid SouthernDenmark = Guid.Parse("D0000000-0000-0000-0000-000000000003");

    private DistrictRepository Repository => new(fixture.ConnectionString);

    [Fact]
    public async Task GetAll_returns_the_seeded_districts_sorted_by_name()
    {
        var names = (await Repository.GetAllAsync()).Select(d => d.Name).ToList();

        names.ShouldBe(names.OrderBy(n => n, StringComparer.Ordinal));
        names.ShouldContain("Capital Region");
        names.ShouldContain("Central Denmark");
        names.ShouldContain("North Denmark");
        names.ShouldContain("Southern Denmark");
        names.ShouldContain("Zealand");
    }

    [Fact]
    public async Task Get_returns_salespersons_primary_first_and_stores()
    {
        var district = await Repository.GetAsync(CapitalRegion);

        district.ShouldNotBeNull();
        district.Name.ShouldBe("Capital Region");
        district.Salespersons.Select(s => s.Name).ShouldBe(["Sofie Pedersen", "Jonas Andersen"]);
        district.Salespersons.Select(s => s.IsPrimary).ShouldBe([true, false]);
        district.Stores.Select(s => s.Name).ShouldBe(["Fields", "Lyngby Storcenter"]);
    }

    [Fact]
    public async Task Get_returns_a_district_with_no_secondaries()
    {
        var district = await Repository.GetAsync(SouthernDenmark);

        district.ShouldNotBeNull();
        district.Salespersons.ShouldHaveSingleItem().IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_id()
    {
        (await Repository.GetAsync(Guid.NewGuid())).ShouldBeNull();
    }
}
