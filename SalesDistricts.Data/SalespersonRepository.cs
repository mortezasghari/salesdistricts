using Dapper;
using Microsoft.Data.SqlClient;
using SalesDistricts.Core;

namespace SalesDistricts.Data;

public sealed class SalespersonRepository(string connectionString) : ISalespersonRepository
{
    public async Task<IReadOnlyList<Salesperson>> GetAllAsync()
    {
        await using var db = new SqlConnection(connectionString);
        return (await db.QueryAsync<Salesperson>("SELECT Id, Name, Email FROM sales.Salesperson ORDER BY Name")).ToList();
    }
}
