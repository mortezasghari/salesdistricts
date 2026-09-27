namespace SalesDistricts.Core;

public enum Role { Primary, Secondary }

public enum AssignmentResult
{
    Ok,
    DistrictNotFound,
    SalespersonNotFound,
    /// <summary>The salesperson is the district's primary. Assign another primary first.</summary>
    SalespersonIsPrimary,
    NotAssigned,
}

/// <summary>
/// Business rules for adding and removing salespersons on a district (Task 3).
/// The database guarantees "exactly one primary"; this class makes the outcomes explicit so the API
/// can explain a refusal instead of surfacing a constraint error.
/// </summary>
public sealed class DistrictAssignments(IDistrictRepository districts, IDistrictAssignmentRepository assignments)
{
    public async Task<AssignmentResult> AssignAsync(Guid districtId, Guid salespersonId, Role role)
    {
        var district = await districts.GetAsync(districtId);
        if (district is null)
        {
            return AssignmentResult.DistrictNotFound;
        }

        if (!await assignments.SalespersonExistsAsync(salespersonId))
        {
            return AssignmentResult.SalespersonNotFound;
        }

        var current = district.Salespersons.FirstOrDefault(s => s.Id == salespersonId);

        if (role == Role.Primary)
        {
            if (current is not { IsPrimary: true })
            {
                await assignments.SetPrimaryAsync(districtId, salespersonId);
            }

            return AssignmentResult.Ok;
        }

        if (current is { IsPrimary: true })
        {
            return AssignmentResult.SalespersonIsPrimary; // a district can never be without a primary
        }

        if (current is null)
        {
            await assignments.AddSecondaryAsync(districtId, salespersonId);
        }

        return AssignmentResult.Ok;
    }

    public async Task<AssignmentResult> RemoveAsync(Guid districtId, Guid salespersonId)
    {
        var district = await districts.GetAsync(districtId);
        if (district is null)
        {
            return AssignmentResult.DistrictNotFound;
        }

        var current = district.Salespersons.FirstOrDefault(s => s.Id == salespersonId);
        if (current is null)
        {
            return AssignmentResult.NotAssigned;
        }

        if (current.IsPrimary)
        {
            return AssignmentResult.SalespersonIsPrimary;
        }

        await assignments.RemoveSecondaryAsync(districtId, salespersonId);
        return AssignmentResult.Ok;
    }
}
