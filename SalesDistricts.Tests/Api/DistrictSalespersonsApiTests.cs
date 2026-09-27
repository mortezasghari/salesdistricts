using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using SalesDistricts.Core;
using SalesDistricts.Tests.Database;

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
        Assert.Contains(salespersons!, s => s.Name == "Sofie Pedersen");
    }

    [Fact]
    public async Task Put_adds_a_secondary()
    {
        var (district, _, _, unassigned) = await SetupAsync();
        var client = Client;

        var response = await client.PutAsJsonAsync($"/districts/{district}/salespersons/{unassigned}", new AssignRequest(Role.Secondary));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False((await RolesAsync(client, district))[unassigned]);
    }

    [Fact]
    public async Task Put_primary_swaps_roles_with_the_previous_primary()
    {
        var (district, primary, secondary, _) = await SetupAsync();
        var client = Client;

        var response = await client.PutAsJsonAsync($"/districts/{district}/salespersons/{secondary}", new AssignRequest(Role.Primary));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var roles = await RolesAsync(client, district);
        Assert.True(roles[secondary]);
        Assert.False(roles[primary]);
        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public async Task Put_secondary_for_the_primary_is_a_conflict()
    {
        var (district, primary, _, _) = await SetupAsync();

        var response = await Client.PutAsJsonAsync($"/districts/{district}/salespersons/{primary}", new AssignRequest(Role.Secondary));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_a_secondary()
    {
        var (district, _, secondary, _) = await SetupAsync();
        var client = Client;

        var response = await client.DeleteAsync($"/districts/{district}/salespersons/{secondary}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(secondary, (await RolesAsync(client, district)).Keys);
    }

    [Fact]
    public async Task Delete_of_the_primary_is_a_conflict()
    {
        var (district, primary, _, _) = await SetupAsync();

        var response = await Client.DeleteAsync($"/districts/{district}/salespersons/{primary}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Assign another primary first", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_ids_are_not_found()
    {
        var (district, _, _, unassigned) = await SetupAsync();
        var client = Client;

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/districts/{Guid.NewGuid()}/salespersons/{unassigned}", new AssignRequest(Role.Secondary))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/districts/{district}/salespersons/{Guid.NewGuid()}", new AssignRequest(Role.Secondary))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/districts/{district}/salespersons/{unassigned}")).StatusCode);
    }
}
