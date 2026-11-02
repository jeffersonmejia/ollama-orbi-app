using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.IO;
using OrbiApp.Data;
using OrbiApp.Models.Delivery;
using Xunit;

namespace OrbiApp.Database.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresDatabaseCollection : ICollectionFixture<PostgresDatabaseFixture>
{
    public const string Name = "isolated-postgres-database";
}

public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly string _connectionString;

    public PostgresDatabaseFixture()
    {
        _connectionString = Environment.GetEnvironmentVariable("ORBI_DATABASE_TEST_CONNECTION")
            ?? throw new InvalidOperationException(
                "Set ORBI_DATABASE_TEST_CONNECTION to the isolated local audit database; production databases are rejected.");

        var connection = new NpgsqlConnectionStringBuilder(_connectionString);
        if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
            connection.Port != 55433 ||
            connection.Database != "orbi_audit" ||
            connection.Username != "orbi_app")
        {
            throw new InvalidOperationException(
                "Database tests are restricted to the local orbi_audit database on port 55433 as orbi_app.");
        }

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString, postgres => postgres.CommandTimeout(30))
            .Options;

        TestDataPrefix = $"dbtest-{Guid.NewGuid():N}-";
    }

    public string TestDataPrefix { get; }
    public string CityCode { get; private set; } = string.Empty;

    public ApplicationDbContext CreateContext() => new(_options);

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.Database.OpenConnectionAsync();

        var sqlDirectory = FindSqlDirectory();
        var connection = db.Database.GetDbConnection();
        await using (var command = connection.CreateCommand())
        {
            foreach (var fileName in new[] { "orbi-schema.sql", "orbi-locations.sql" })
            {
                command.CommandText = await File.ReadAllTextAsync(Path.Combine(sqlDirectory, fileName));
                await command.ExecuteNonQueryAsync();
            }
        }

        var databaseName = await db.Database.SqlQueryRaw<string>("SELECT current_database() AS \"Value\"").SingleAsync();
        if (databaseName != "orbi_audit")
            throw new InvalidOperationException("Refusing to run database tests outside orbi_audit.");

        CityCode = await db.Set<OrbiApp.Models.Identity.EcuadorCity>()
            .AsNoTracking()
            .OrderBy(city => city.Code)
            .Select(city => city.Code)
            .FirstAsync();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO delivery_store (name, category, address, province_code, city_code, is_active)
            SELECT {TestDataPrefix} || series::text,
                   'Farmacia',
                   'Dirección de prueba',
                   location.province_code,
                   location.city_code,
                   TRUE
            FROM generate_series(1, 80) AS series
            CROSS JOIN (
                SELECT province_code, city_code
                FROM ecuador_city
                ORDER BY city_code
                LIMIT 1
            ) AS location;
            """);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO delivery_product
                (delivery_store_id, name, price, unit_cost, stock, is_available, created_at, updated_at)
            SELECT store.delivery_store_id,
                   CASE WHEN product_no % 100 = 0
                       THEN {TestDataPrefix} || 'catalog-hit-' || product_no::text
                       ELSE {TestDataPrefix} || 'producto-' || product_no::text
                   END,
                   1 + (product_no % 40),
                   0,
                   10,
                   TRUE,
                   now() - (product_no * interval '1 second'),
                   now()
            FROM delivery_store AS store
            CROSS JOIN generate_series(1, 500) AS product_no
            WHERE store.name LIKE {TestDataPrefix + "%"};
            """);

        await db.Database.ExecuteSqlRawAsync("ANALYZE delivery_store; ANALYZE delivery_product;");
    }

    private static string FindSqlDirectory()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var sqlDirectory = Path.Combine(directory.FullName, "src", "Features", "Shared", "Infrastructure", "Sql");
                if (File.Exists(Path.Combine(sqlDirectory, "orbi-schema.sql"))) return sqlDirectory;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository SQL schema directory for database tests.");
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM delivery_product AS product
            USING delivery_store AS store
            WHERE product.delivery_store_id = store.delivery_store_id
              AND store.name LIKE {TestDataPrefix + "%"};
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM delivery_store WHERE name LIKE {TestDataPrefix + "%"};");
        await db.Database.ExecuteSqlRawAsync("ANALYZE delivery_store; ANALYZE delivery_product;");
    }
}
