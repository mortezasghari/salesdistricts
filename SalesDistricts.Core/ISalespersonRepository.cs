namespace SalesDistricts.Core;

public sealed record Salesperson(Guid Id, string Name, string? Email);

public interface ISalespersonRepository
{
    Task<IReadOnlyList<Salesperson>> GetAllAsync();
}
