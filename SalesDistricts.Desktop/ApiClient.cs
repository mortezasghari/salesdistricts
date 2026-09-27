using System.Net.Http.Json;
using SalesDistricts.Core;

namespace SalesDistricts.Desktop;

/// <summary>The desktop app only talks to the API; it never touches the database (single system data ownership).</summary>
public sealed class ApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<District>> GetDistrictsAsync() =>
        await http.GetFromJsonAsync<List<District>>("districts") ?? [];

    public Task<DistrictDetails?> GetDistrictAsync(Guid id) =>
        http.GetFromJsonAsync<DistrictDetails>($"districts/{id}");

    public async Task<IReadOnlyList<Salesperson>> GetSalespersonsAsync() =>
        await http.GetFromJsonAsync<List<Salesperson>>("salespersons") ?? [];

    /// <summary>Returns null on success, otherwise the API's explanation.</summary>
    public async Task<string?> AssignAsync(Guid districtId, Guid salespersonId, Role role) =>
        await ErrorOf(await http.PutAsJsonAsync($"districts/{districtId}/salespersons/{salespersonId}", new { role = role.ToString() }));

    public async Task<string?> RemoveAsync(Guid districtId, Guid salespersonId) =>
        await ErrorOf(await http.DeleteAsync($"districts/{districtId}/salespersons/{salespersonId}"));

    private static async Task<string?> ErrorOf(HttpResponseMessage response) =>
        response.IsSuccessStatusCode ? null : await response.Content.ReadFromJsonAsync<string>() ?? response.ReasonPhrase;
}
