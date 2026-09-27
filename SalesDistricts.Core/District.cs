namespace SalesDistricts.Core;

public sealed record District(Guid Id, string Name);

public sealed record DistrictSalesperson(Guid Id, string Name, string? Email, bool IsPrimary);

public sealed record Store(Guid Id, string Name, string? Address);

public sealed record DistrictDetails(
    Guid Id,
    string Name,
    IReadOnlyList<DistrictSalesperson> Salespersons,
    IReadOnlyList<Store> Stores);
