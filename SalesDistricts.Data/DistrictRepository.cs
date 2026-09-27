using Dapper;
using Microsoft.Data.SqlClient;
using SalesDistricts.Core;

namespace SalesDistricts.Data;

public sealed class DistrictRepository(string connectionString) : IDistrictRepository, IDistrictAssignmentRepository
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

    public async Task<bool> SalespersonExistsAsync(Guid salespersonId)
    {
        await using var db = new SqlConnection(connectionString);
        return await db.ExecuteScalarAsync<bool>("SELECT COUNT(*) FROM sales.Salesperson WHERE Id = @salespersonId", new { salespersonId });
    }

    public async Task SetPrimaryAsync(Guid districtId, Guid salespersonId)
    {
        await using var db = new SqlConnection(connectionString);
        await db.OpenAsync();
        await using var transaction = await db.BeginTransactionAsync();

        await db.ExecuteAsync("""
            DECLARE @previous uniqueidentifier = (SELECT PrimarySalespersonId FROM sales.District WHERE Id = @districtId);

            UPDATE sales.District SET PrimarySalespersonId = @salespersonId WHERE Id = @districtId;

            -- The new primary must not also be listed as a secondary.
            DELETE FROM sales.DistrictSecondarySalesperson WHERE DistrictId = @districtId AND SalespersonId = @salespersonId;

            -- The previous primary stays on the district as a secondary.
            IF @previous <> @salespersonId
               AND NOT EXISTS (SELECT 1 FROM sales.DistrictSecondarySalesperson WHERE DistrictId = @districtId AND SalespersonId = @previous)
                INSERT INTO sales.DistrictSecondarySalesperson (DistrictId, SalespersonId) VALUES (@districtId, @previous);
            """, new { districtId, salespersonId }, transaction);

        await transaction.CommitAsync();
    }

    public async Task AddSecondaryAsync(Guid districtId, Guid salespersonId)
    {
        await using var db = new SqlConnection(connectionString);
        await db.ExecuteAsync("INSERT INTO sales.DistrictSecondarySalesperson (DistrictId, SalespersonId) VALUES (@districtId, @salespersonId)",
            new { districtId, salespersonId });
    }

    public async Task RemoveSecondaryAsync(Guid districtId, Guid salespersonId)
    {
        await using var db = new SqlConnection(connectionString);
        await db.ExecuteAsync("DELETE FROM sales.DistrictSecondarySalesperson WHERE DistrictId = @districtId AND SalespersonId = @salespersonId",
            new { districtId, salespersonId });
    }
}
