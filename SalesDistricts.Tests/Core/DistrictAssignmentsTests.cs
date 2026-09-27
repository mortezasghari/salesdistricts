using Moq;
using SalesDistricts.Core;
using Shouldly;

namespace SalesDistricts.Tests.Core;

/// <summary>Rules only; repositories are mocked. Database behaviour is covered in Database/.</summary>
public sealed class DistrictAssignmentsTests
{
    private static readonly Guid North = Guid.NewGuid();
    private static readonly Guid Anders = Guid.NewGuid(); // primary of North
    private static readonly Guid Mette = Guid.NewGuid();  // secondary in North
    private static readonly Guid Lars = Guid.NewGuid();   // not assigned

    private readonly Mock<IDistrictRepository> _districts = new();
    private readonly Mock<IDistrictAssignmentRepository> _assignments = new();

    private DistrictAssignments Sut => new(_districts.Object, _assignments.Object);

    public DistrictAssignmentsTests()
    {
        _districts.Setup(d => d.GetAsync(North)).ReturnsAsync(new DistrictDetails(North, "North Denmark",
            [new DistrictSalesperson(Anders, "Anders", null, IsPrimary: true), new DistrictSalesperson(Mette, "Mette", null, IsPrimary: false)],
            []));
        _assignments.Setup(a => a.SalespersonExistsAsync(It.IsIn(Anders, Mette, Lars))).ReturnsAsync(true);
    }

    [Fact]
    public async Task Adding_a_new_secondary_adds_them()
    {
        var result = await Sut.AssignAsync(North, Lars, Role.Secondary);

        result.ShouldBe(AssignmentResult.Ok);
        _assignments.Verify(a => a.AddSecondaryAsync(North, Lars), Times.Once);
    }

    [Fact]
    public async Task Adding_an_existing_secondary_again_is_a_no_op()
    {
        var result = await Sut.AssignAsync(North, Mette, Role.Secondary);

        result.ShouldBe(AssignmentResult.Ok);
        _assignments.Verify(a => a.AddSecondaryAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task The_primary_cannot_be_demoted_to_secondary()
    {
        var result = await Sut.AssignAsync(North, Anders, Role.Secondary);

        result.ShouldBe(AssignmentResult.SalespersonIsPrimary);
        VerifyNoWrites();
    }

    [Fact]
    public async Task Making_someone_primary_delegates_to_the_atomic_operation()
    {
        var result = await Sut.AssignAsync(North, Mette, Role.Primary);

        result.ShouldBe(AssignmentResult.Ok);
        _assignments.Verify(a => a.SetPrimaryAsync(North, Mette), Times.Once);
    }

    [Fact]
    public async Task Making_the_current_primary_primary_again_is_a_no_op()
    {
        var result = await Sut.AssignAsync(North, Anders, Role.Primary);

        result.ShouldBe(AssignmentResult.Ok);
        VerifyNoWrites();
    }

    [Fact]
    public async Task Unknown_district_or_salesperson_are_reported()
    {
        (await Sut.AssignAsync(Guid.NewGuid(), Lars, Role.Secondary)).ShouldBe(AssignmentResult.DistrictNotFound);
        (await Sut.AssignAsync(North, Guid.NewGuid(), Role.Secondary)).ShouldBe(AssignmentResult.SalespersonNotFound);
        (await Sut.RemoveAsync(Guid.NewGuid(), Mette)).ShouldBe(AssignmentResult.DistrictNotFound);
        VerifyNoWrites();
    }

    [Fact]
    public async Task Removing_a_secondary_removes_them()
    {
        var result = await Sut.RemoveAsync(North, Mette);

        result.ShouldBe(AssignmentResult.Ok);
        _assignments.Verify(a => a.RemoveSecondaryAsync(North, Mette), Times.Once);
    }

    [Fact]
    public async Task Removing_the_primary_is_refused()
    {
        var result = await Sut.RemoveAsync(North, Anders);

        result.ShouldBe(AssignmentResult.SalespersonIsPrimary);
        VerifyNoWrites();
    }

    [Fact]
    public async Task Removing_someone_not_on_the_district_is_reported()
    {
        var result = await Sut.RemoveAsync(North, Lars);

        result.ShouldBe(AssignmentResult.NotAssigned);
        VerifyNoWrites();
    }

    private void VerifyNoWrites()
    {
        _assignments.Verify(a => a.SetPrimaryAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        _assignments.Verify(a => a.AddSecondaryAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        _assignments.Verify(a => a.RemoveSecondaryAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }
}
