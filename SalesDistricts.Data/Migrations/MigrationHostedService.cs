using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SalesDistricts.Data.Migrations;

/// <summary>Applies pending migrations while the host starts, before requests are served.</summary>
public sealed class MigrationHostedService(Migrator migrator, ILogger<MigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var appliedNow = await migrator.UpAsync();
        var status = await migrator.StatusAsync();
        logger.LogInformation("Migrations: {AppliedNow} applied now, {Applied} applied in total, {Pending} pending",
            appliedNow, status.Applied.Count, status.Pending.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
