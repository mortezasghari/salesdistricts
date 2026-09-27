using SalesDistricts.Data;

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
        var districts = await Repository.GetAllAsync();

        var names = districts.Select(d => d.Name).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
        Assert.All(new[] { "Capital Region", "Central Denmark", "North Denmark", "Southern Denmark", "Zealand" },
            expected => Assert.Contains(expected, names));
    }

    [Fact]
    public async Task Get_returns_salespersons_primary_first_and_stores()
    {
        var district = await Repository.GetAsync(CapitalRegion);

        Assert.NotNull(district);
        Assert.Equal("Capital Region", district.Name);
        Assert.Equal(["Sofie Pedersen", "Jonas Andersen"], district.Salespersons.Select(s => s.Name));
        Assert.Equal([true, false], district.Salespersons.Select(s => s.IsPrimary));
        Assert.Equal(["Fields", "Lyngby Storcenter"], district.Stores.Select(s => s.Name));
    }

    [Fact]
    public async Task Get_returns_a_district_with_no_secondaries()
    {
        var district = await Repository.GetAsync(SouthernDenmark);

        Assert.NotNull(district);
        Assert.Single(district.Salespersons);
        Assert.True(district.Salespersons[0].IsPrimary);
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_id()
    {
        Assert.Null(await Repository.GetAsync(Guid.NewGuid()));
    }
}
