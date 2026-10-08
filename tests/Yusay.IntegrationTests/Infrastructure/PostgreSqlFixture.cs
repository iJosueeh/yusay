using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
using Yusay.Application.Common.Interfaces;
using Yusay.Infrastructure.Persistence;

namespace Yusay.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("yusay_test")
        .WithUsername("postgres")
        .WithPassword("test_password")
        .Build();

    public IDbConnectionFactory ConnectionFactory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString() + ";Search Path=yusay,pg_temp;";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
        var dataSource = dataSourceBuilder.Build();
        ConnectionFactory = new NpgsqlConnectionFactory(dataSource);

        await ApplyMigrationsAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        var migrationsDir = FindMigrationsPath();
        var migrationFiles = Directory.GetFiles(migrationsDir, "V*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync();

        foreach (var file in migrationFiles)
        {
            var sql = await File.ReadAllTextAsync(file);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string FindMigrationsPath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null && !File.Exists(Path.Combine(current.FullName, "Yusay.slnx")))
        {
            current = current.Parent;
        }

        if (current == null)
        {
            throw new InvalidOperationException("No se encontró la raíz del proyecto para localizar las migraciones.");
        }

        return Path.Combine(current.FullName, "database", "migrations");
    }
}

[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>
{
}
