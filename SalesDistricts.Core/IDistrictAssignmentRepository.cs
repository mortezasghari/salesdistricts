namespace SalesDistricts.Core;

/// <summary>Write side of district/salesperson assignments. Each call is one atomic database operation.</summary>
public interface IDistrictAssignmentRepository
{
    Task<bool> SalespersonExistsAsync(Guid salespersonId);

    /// <summary>
    /// Makes the salesperson the district's primary. In the same transaction the previous primary
    /// becomes a secondary, and the new primary is removed from the secondaries if listed there.
    /// </summary>
    Task SetPrimaryAsync(Guid districtId, Guid salespersonId);

    Task AddSecondaryAsync(Guid districtId, Guid salespersonId);

    Task RemoveSecondaryAsync(Guid districtId, Guid salespersonId);
}
