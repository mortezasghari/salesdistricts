using Dapper;
using Microsoft.Data.SqlClient;
using SalesDistricts.Core;

namespace SalesDistricts.Data;

public sealed class DistrictRepository(string connectionString) : IDistrictRepository
{
    public async Task<IReadOnlyList<District>> GetAllAsync()
    {
        await using var db = new SqlConnection(connectionString);
        return (await db.QueryAsync<District>("SELECT Id, Name FROM sales.District ORDER BY Name")).ToList();
    }

    public async Task<DistrictDetails?> GetAsync(Guid id)
    {
        await using var db = new SqlConnection(connectionString);
        await using var result = await db.QueryMultipleAsync("""
            SELECT Id, Name FROM sales.District WHERE Id = @id;

            SELECT s.Id, s.Name, s.Email, a.IsPrimary
            FROM   sales.vDistrictSalesperson a
            JOIN   sales.Salesperson s ON s.Id = a.SalespersonId
            WHERE  a.DistrictId = @id
            ORDER BY a.IsPrimary DESC, s.Name;

            SELECT Id, Name, Address FROM sales.Store WHERE DistrictId = @id ORDER BY Name;
            """, new { id });

        var district = await result.ReadSingleOrDefaultAsync<District>();
        if (district is null)
        {
            return null;
        }

        var salespersons = (await result.ReadAsync<DistrictSalesperson>()).ToList();
        var stores = (await result.ReadAsync<Store>()).ToList();
        return new DistrictDetails(district.Id, district.Name, salespersons, stores);
    }
}
