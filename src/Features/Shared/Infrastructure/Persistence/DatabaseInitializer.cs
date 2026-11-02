using Microsoft.EntityFrameworkCore;

namespace OrbiApp.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, string contentRootPath, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var identityDb = provider.GetRequiredService<ApplicationDbContext>();
        await identityDb.Database.MigrateAsync(cancellationToken);

        var sqlDirectory = Path.Combine(contentRootPath, "db");
        if (!Directory.Exists(sqlDirectory))
        {
            sqlDirectory = Path.Combine(AppContext.BaseDirectory, "db");
        }

        var connection = identityDb.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        foreach (var fileName in new[] { "orbi-schema.sql", "orbi-locations.sql" })
        {
            var sqlPath = Path.Combine(sqlDirectory, fileName);
            if (!File.Exists(sqlPath)) continue;
            command.CommandText = await File.ReadAllTextAsync(sqlPath, cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await IdentitySeeder.SeedAsync(provider);
        var dataPath = Path.Combine(sqlDirectory, "orbi-data.sql");
        if (File.Exists(dataPath))
        {
            command.CommandText = await File.ReadAllTextAsync(dataPath, cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
