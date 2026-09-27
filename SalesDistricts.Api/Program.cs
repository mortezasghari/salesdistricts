using System.Text.Json.Serialization;
using SalesDistricts.Core;
using SalesDistricts.Data;
using SalesDistricts.Data.Migrations;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SalesDistricts")
    ?? throw new InvalidOperationException("ConnectionStrings:SalesDistricts is not configured");

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddMigrations(connectionString);

var districts = new DistrictRepository(connectionString);
builder.Services.AddSingleton<IDistrictRepository>(districts);
builder.Services.AddSingleton<IDistrictAssignmentRepository>(districts);
builder.Services.AddSingleton<ISalespersonRepository>(new SalespersonRepository(connectionString));
builder.Services.AddSingleton<DistrictAssignments>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/migrations", (Migrator migrator) => migrator.StatusAsync());

app.MapGet("/salespersons", (ISalespersonRepository salespersons) => salespersons.GetAllAsync());

app.MapGet("/districts", (IDistrictRepository districts) => districts.GetAllAsync());

app.MapGet("/districts/{id:guid}", async (Guid id, IDistrictRepository districts) =>
    await districts.GetAsync(id) is { } district ? Results.Ok(district) : Results.NotFound());

// Assigns the salesperson to the district with the given role, or changes their role. Idempotent.
app.MapPut("/districts/{id:guid}/salespersons/{salespersonId:guid}",
    async (Guid id, Guid salespersonId, AssignRequest request, DistrictAssignments assignments) =>
        ToResult(await assignments.AssignAsync(id, salespersonId, request.Role)));

app.MapDelete("/districts/{id:guid}/salespersons/{salespersonId:guid}",
    async (Guid id, Guid salespersonId, DistrictAssignments assignments) =>
        ToResult(await assignments.RemoveAsync(id, salespersonId)));

app.Run();

static IResult ToResult(AssignmentResult result) => result switch
{
    AssignmentResult.Ok => Results.NoContent(),
    AssignmentResult.DistrictNotFound => Results.NotFound("District not found."),
    AssignmentResult.SalespersonNotFound => Results.NotFound("Salesperson not found."),
    AssignmentResult.NotAssigned => Results.NotFound("Salesperson is not assigned to this district."),
    AssignmentResult.SalespersonIsPrimary => Results.Conflict("Salesperson is the district's primary. Assign another primary first."),
    _ => Results.StatusCode(500),
};

public sealed record AssignRequest(Role Role);

public partial class Program;
