using SalesDistricts.Core;

namespace SalesDistricts.Tests.Core;

/// <summary>Rules only; the repository is an in-memory fake. Database behaviour is covered in Database/.</summary>
public sealed class DistrictAssignmentsTests
{
    private static readonly Guid North = Guid.NewGuid();
    private static readonly Guid Anders = Guid.NewGuid();
    private static readonly Guid Mette = Guid.NewGuid();
    private static readonly Guid Lars = Guid.NewGuid();

    private readonly FakeRepository _repo = new(primary: Anders, secondaries: [Mette], known: [Anders, Mette, Lars]);
    private DistrictAssignments Sut => new(_repo, _repo);

    [Fact]
    public async Task Adding_a_new_secondary_adds_them()
    {
        Assert.Equal(AssignmentResult.Ok, await Sut.AssignAsync(North, Lars, Role.Secondary));
        Assert.Equal([Mette, Lars], _repo.Secondaries);
    }

    [Fact]
    public async Task Adding_an_existing_secondary_again_is_a_no_op()
    {
        Assert.Equal(AssignmentResult.Ok, await Sut.AssignAsync(North, Mette, Role.Secondary));
        Assert.Equal([Mette], _repo.Secondaries);
    }

    [Fact]
    public async Task The_primary_cannot_be_demoted_to_secondary()
    {
        Assert.Equal(AssignmentResult.SalespersonIsPrimary, await Sut.AssignAsync(North, Anders, Role.Secondary));
        Assert.Equal(Anders, _repo.Primary);
    }

    [Fact]
    public async Task Making_someone_primary_delegates_to_the_atomic_operation()
    {
        Assert.Equal(AssignmentResult.Ok, await Sut.AssignAsync(North, Mette, Role.Primary));
        Assert.Equal(Mette, _repo.Primary);
        Assert.Equal([Anders], _repo.Secondaries); // old primary kept as secondary, new primary no longer a secondary
    }

    [Fact]
    public async Task Making_the_current_primary_primary_again_is_a_no_op()
    {
        Assert.Equal(AssignmentResult.Ok, await Sut.AssignAsync(North, Anders, Role.Primary));
        Assert.Equal(0, _repo.SetPrimaryCalls);
    }

    [Fact]
    public async Task Unknown_district_or_salesperson_are_reported()
    {
        Assert.Equal(AssignmentResult.DistrictNotFound, await Sut.AssignAsync(Guid.NewGuid(), Lars, Role.Secondary));
        Assert.Equal(AssignmentResult.SalespersonNotFound, await Sut.AssignAsync(North, Guid.NewGuid(), Role.Secondary));
        Assert.Equal(AssignmentResult.DistrictNotFound, await Sut.RemoveAsync(Guid.NewGuid(), Mette));
    }

    [Fact]
    public async Task Removing_a_secondary_removes_them()
    {
        Assert.Equal(AssignmentResult.Ok, await Sut.RemoveAsync(North, Mette));
        Assert.Empty(_repo.Secondaries);
    }

    [Fact]
    public async Task Removing_the_primary_is_refused()
    {
        Assert.Equal(AssignmentResult.SalespersonIsPrimary, await Sut.RemoveAsync(North, Anders));
        Assert.Equal(Anders, _repo.Primary);
    }

    [Fact]
    public async Task Removing_someone_not_on_the_district_is_reported()
    {
        Assert.Equal(AssignmentResult.NotAssigned, await Sut.RemoveAsync(North, Lars));
    }

    private sealed class FakeRepository(Guid primary, List<Guid> secondaries, Guid[] known)
        : IDistrictRepository, IDistrictAssignmentRepository
    {
        public Guid Primary { get; private set; } = primary;
        public List<Guid> Secondaries { get; } = secondaries;
        public int SetPrimaryCalls { get; private set; }

        public Task<IReadOnlyList<District>> GetAllAsync() => throw new NotSupportedException();

        public Task<DistrictDetails?> GetAsync(Guid id)
        {
            if (id != North) return Task.FromResult<DistrictDetails?>(null);
            var people = new[] { new DistrictSalesperson(Primary, "", null, true) }
                .Concat(Secondaries.Select(s => new DistrictSalesperson(s, "", null, false)))
                .ToList();
            return Task.FromResult<DistrictDetails?>(new DistrictDetails(id, "North", people, []));
        }

        public Task<bool> SalespersonExistsAsync(Guid salespersonId) => Task.FromResult(known.Contains(salespersonId));

        public Task SetPrimaryAsync(Guid districtId, Guid salespersonId)
        {
            SetPrimaryCalls++;
            Secondaries.Remove(salespersonId);
            Secondaries.Add(Primary);
            Primary = salespersonId;
            return Task.CompletedTask;
        }

        public Task AddSecondaryAsync(Guid districtId, Guid salespersonId) { Secondaries.Add(salespersonId); return Task.CompletedTask; }

        public Task RemoveSecondaryAsync(Guid districtId, Guid salespersonId) { Secondaries.Remove(salespersonId); return Task.CompletedTask; }
    }
}
