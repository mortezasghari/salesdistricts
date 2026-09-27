using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace SalesDistricts.Data.Migrations;

/// <summary>A versioned schema change: Scripts/Ups/{Version:0000}_{Name}.sql and Scripts/Downs/{Version:0000}_{Name}.sql.</summary>
public sealed record Migration(long Version, string Name, string Up, string Down);
public sealed record AppliedMigration(long Version, string Name, DateTime AppliedAtUtc);
public sealed record PendingMigration(long Version, string Name);
public sealed record MigrationStatus(IReadOnlyList<AppliedMigration> Applied, IReadOnlyList<PendingMigration> Pending);

/// <summary>
/// Applies and reverts migrations, journaling each one in dbo.SchemaVersion.
/// One transaction per migration: the SQL and the journal row commit or roll back together.
/// A line containing only GO separates batches.
/// </summary>
public sealed class Migrator(string connectionString, IReadOnlyList<Migration>? migrations = null)
{
    private static readonly Assembly Scripts = typeof(Migrator).Assembly;
    private readonly IReadOnlyList<Migration> _migrations = (migrations ?? LoadEmbedded()).OrderBy(m => m.Version).ToArray();

    public async Task<MigrationStatus> StatusAsync()
    {
        await using var db = await OpenAsync();
        var applied = (await db.QueryAsync<AppliedMigration>("SELECT Version, Name, AppliedAtUtc FROM dbo.SchemaVersion ORDER BY Version")).ToList();
        var appliedVersions = applied.Select(a => a.Version).ToHashSet();
        var pending = _migrations.Where(m => !appliedVersions.Contains(m.Version)).Select(m => new PendingMigration(m.Version, m.Name)).ToList();
        return new MigrationStatus(applied, pending);
    }

    /// <summary>Applies every pending migration, oldest first. Returns how many were applied.</summary>
    public async Task<int> UpAsync()
    {
        await using var db = await OpenAsync();
        var applied = (await db.QueryAsync<long>("SELECT Version FROM dbo.SchemaVersion")).ToHashSet();

        var count = 0;
        foreach (var migration in _migrations.Where(m => !applied.Contains(m.Version)))
        {
            await RunAsync(db, migration, migration.Up, "INSERT INTO dbo.SchemaVersion (Version, Name) VALUES (@Version, @Name)");
            count++;
        }

        return count;
    }

    /// <summary>Reverts applied migrations newer than <paramref name="targetVersion"/>, newest first. 0 reverts everything but the journal. Returns how many were reverted.</summary>
    public async Task<int> DownAsync(long targetVersion = 0)
    {
        await using var db = await OpenAsync();
        var applied = (await db.QueryAsync<long>("SELECT Version FROM dbo.SchemaVersion")).ToHashSet();

        var count = 0;
        foreach (var migration in _migrations.Where(m => applied.Contains(m.Version) && m.Version > targetVersion).Reverse())
        {
            await RunAsync(db, migration, migration.Down, "DELETE FROM dbo.SchemaVersion WHERE Version = @Version");
            count++;
        }

        return count;
    }

    private static async Task RunAsync(SqlConnection db, Migration migration, string sql, string journalSql)
    {
        await using var transaction = await db.BeginTransactionAsync();

        foreach (var batch in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                await db.ExecuteAsync(batch, transaction: transaction);
            }
        }

        await db.ExecuteAsync(journalSql, new { migration.Version, migration.Name }, transaction);
        await transaction.CommitAsync();
    }

    private async Task<SqlConnection> OpenAsync()
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var name = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.ExecuteAsync(Read("CreateDatabase.sql"), new { name });
        }

        var db = new SqlConnection(connectionString);
        await db.OpenAsync();
        await db.ExecuteAsync(Read("Ups/0000_SchemaVersion.sql")); // journal table, idempotent
        return db;
    }

    private static Migration[] LoadEmbedded() =>
        Scripts.GetManifestResourceNames()
            .Where(resource => resource.StartsWith("Ups/"))
            .Select(resource => Path.GetFileNameWithoutExtension(resource)) // 0001_InitialSchema
            .Select(file => new Migration(
                Version: long.Parse(file[..file.IndexOf('_')]),
                Name: file[(file.IndexOf('_') + 1)..],
                Up: Read($"Ups/{file}.sql"),
                Down: Read($"Downs/{file}.sql")))
            .ToArray();

    private static string Read(string resource)
    {
        using var stream = Scripts.GetManifestResourceStream(resource)
                           ?? throw new FileNotFoundException($"Embedded script '{resource}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
