using Microsoft.Extensions.DependencyInjection;

namespace SalesDistricts.Data.Migrations;

public static class MigrationServiceCollectionExtensions
{
    public static IServiceCollection AddMigrations(this IServiceCollection services, string connectionString) =>
        services
            .AddSingleton(new Migrator(connectionString))
            .AddHostedService<MigrationHostedService>();
}
