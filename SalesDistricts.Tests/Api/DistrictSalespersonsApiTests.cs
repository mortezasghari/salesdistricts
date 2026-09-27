using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using SalesDistricts.Core;
using SalesDistricts.Tests.Database;
using Shouldly;

namespace SalesDistricts.Tests.Api;

/// <summary>End-to-end through the real API against the Testcontainers database. Each test sets up its own district.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class DistrictSalespersonsApiTests(SqlServerFixture fixture)
{
    private HttpClient Client => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:SalesDistricts", fixture.ConnectionString))
        .CreateClient();

    /// <summary>A district with one primary and one secondary, plus a third unassigned salesperson.</summary>
    private async Task<(Guid District, Guid Primary, Guid Secondary, Guid Unassigned)> SetupAsync()
    {
        using var c = fixture.Open();
        const string person = "INSERT INTO sales.Salesperson (Name) OUTPUT inserted.Id VALUES (@name)";
        var primary = await c.QuerySingleAsync<Guid>(person, new { name = $"Primary {Guid.NewGuid():N}" });
        var secondary = await c.QuerySingleAsync<Guid>(person, new { name = $"Secondary {Guid.NewGuid():N}" });
        var unassigned = await c.QuerySingleAsync<Guid>(person, new { name = $"Unassigned {Guid.NewGuid():N}" });
        var district = await c.QuerySingleAsync<Guid>(
            "INSERT INTO sales.District (Name, PrimarySalespersonId) OUTPUT inserted.Id VALUES (@name, @primary)",
            new { name = $"District {Guid.NewGuid():N}", primary });
        await c.ExecuteAsync("INSERT INTO sales.DistrictSecondarySalesperson VALUES (@district, @secondary)", new { district, secondary });
        return (district, primary, secondary, unassigned);
    }

    private static async Task<Dictionary<Guid, bool>> RolesAsync(HttpClient client, Guid district)
    {
        var details = await client.GetFromJsonAsync<DistrictDetails>($"/districts/{district}");
        return details!.Salespersons.ToDictionary(s => s.Id, s => s.IsPrimary);
    }

    [Fact]
    public async Task Salespersons_can_be_listed()
    {
        var salespersons = await Client.GetFromJsonAsync<List<Salesperson>>("/salespersons");

        salespersons.ShouldNotBeNull();
        salespersons.ShouldContain(s => s.Name == "Sofie Pedersen");
    }

    [Fact]
    public async Task Put_adds_a_secondary()
    {
        var (district, _, _, unassigned) = await SetupAsync();
        var client = Client;

        var response = await client.PutAsJsonAsync($"/districts/{district}/salespersons/{unassigned}", new AssignRequest(Role.Secondary));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RolesAsync(client, district))[unassigned].ShouldBeFalse();
    }

    [Fact]
    public async Task Put_primary_swaps_roles_with_the_previous_primary()
    {
        var (district, primary, secondary, _) = await SetupAsync();
        var client = Client;

        var response = await client.PutAsJsonAsync($"/districts/{district}/salespersons/{secondary}", new AssignRequest(Role.Primary));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var roles = await RolesAsync(client, district);
        roles[secondary].ShouldBeTrue();
        roles[primary].ShouldBeFalse();
        roles.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Put_secondary_for_the_primary_is_a_conflict()
    {
        var (district, primary, _, _) = await SetupAsync();

        var response = await Client.PutAsJsonAsync($"/districts/{district}/salespersons/{primary}", new AssignRequest(Role.Secondary));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_removes_a_secondary()
    {
        var (district, _, secondary, _) = await SetupAsync();
        var client = Client;

        var response = await client.DeleteAsync($"/districts/{district}/salespersons/{secondary}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RolesAsync(client, district)).Keys.ShouldNotContain(secondary);
    }

    [Fact]
    public async Task Delete_of_the_primary_is_a_conflict()
    {
        var (district, primary, _, _) = await SetupAsync();

        var response = await Client.DeleteAsync($"/districts/{district}/salespersons/{primary}");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Assign another primary first");
    }

    [Fact]
    public async Task Unknown_ids_are_not_found()
    {
        var (district, _, _, unassigned) = await SetupAsync();
        var client = Client;

        (await client.PutAsJsonAsync($"/districts/{Guid.NewGuid()}/salespersons/{unassigned}", new AssignRequest(Role.Secondary)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/districts/{district}/salespersons/{Guid.NewGuid()}", new AssignRequest(Role.Secondary)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/districts/{district}/salespersons/{unassigned}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
