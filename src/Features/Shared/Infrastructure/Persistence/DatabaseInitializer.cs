using Microsoft.EntityFrameworkCore;

namespace OrbiApp.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, string contentRootPath, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var identityDb = provider.GetRequiredService<ApplicationDbContext>();
        var connection = identityDb.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.CommandText = "SELECT pg_advisory_lock(728314902);";
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        try
        {
            await identityDb.Database.MigrateAsync(cancellationToken);

            var sqlDirectory = Path.Combine(contentRootPath, "db");
            if (!Directory.Exists(sqlDirectory))
            {
                sqlDirectory = Path.Combine(AppContext.BaseDirectory, "db");
            }

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
        finally
        {
            await using var unlockCommand = connection.CreateCommand();
            unlockCommand.CommandText = "SELECT pg_advisory_unlock(728314902);";
            await unlockCommand.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }
}
