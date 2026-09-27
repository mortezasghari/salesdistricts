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
}
