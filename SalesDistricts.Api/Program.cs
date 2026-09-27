using SalesDistricts.Core;
using SalesDistricts.Data;
using SalesDistricts.Data.Migrations;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("SalesDistricts")
    ?? throw new InvalidOperationException("ConnectionStrings:SalesDistricts is not configured");

builder.Services.AddOpenApi();
builder.Services.AddMigrations(connectionString);
builder.Services.AddSingleton<IDistrictRepository>(new DistrictRepository(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/migrations", (Migrator migrator) => migrator.StatusAsync());

app.MapGet("/districts", (IDistrictRepository districts) => districts.GetAllAsync());

app.MapGet("/districts/{id:guid}", async (Guid id, IDistrictRepository districts) =>
    await districts.GetAsync(id) is { } district ? Results.Ok(district) : Results.NotFound());

app.Run();
