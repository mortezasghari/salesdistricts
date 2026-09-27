using SalesDistricts.Data.Migrations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMigrations(builder.Configuration.GetConnectionString("SalesDistricts")
    ?? throw new InvalidOperationException("ConnectionStrings:SalesDistricts is not configured"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/migrations", (Migrator migrator) => migrator.StatusAsync());

app.Run();
