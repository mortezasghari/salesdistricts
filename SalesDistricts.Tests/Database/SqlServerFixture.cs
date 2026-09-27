using Microsoft.Data.SqlClient;
using SalesDistricts.Data.Migrations;
using Testcontainers.MsSql;

namespace SalesDistricts.Tests.Database;

/// <summary>One throw-away SQL Server container per test run, migrated with the real migrations.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = ConnectionStringFor("SalesDistricts");
        await new Migrator(ConnectionString).UpAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Connection string for any database on the container; the migrator creates it on first use.</summary>
    public string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    public SqlConnection Open() => Open(ConnectionString);

    public static SqlConnection Open(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
