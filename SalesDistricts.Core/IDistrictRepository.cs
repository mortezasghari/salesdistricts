namespace SalesDistricts.Core;

public interface IDistrictRepository
{
    Task<IReadOnlyList<District>> GetAllAsync();

    /// <summary>The district with its salespersons (primary first) and stores, or null if it does not exist.</summary>
    Task<DistrictDetails?> GetAsync(Guid id);
}
