using Microsoft.EntityFrameworkCore;

namespace OrbiApp.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, string contentRootPath)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var identityDb = provider.GetRequiredService<ApplicationDbContext>();
        await identityDb.Database.MigrateAsync();

        var connection = identityDb.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        foreach (var fileName in new[] { "orbi-schema.sql", "orbi-locations.sql" })
        {
            var sqlPath = Path.Combine(contentRootPath, "db", fileName);
            if (!File.Exists(sqlPath)) continue;
            command.CommandText = await File.ReadAllTextAsync(sqlPath);
            await command.ExecuteNonQueryAsync();
        }

        await IdentitySeeder.SeedAsync(provider);
        var dataPath = Path.Combine(contentRootPath, "db", "orbi-data.sql");
        if (File.Exists(dataPath))
        {
            command.CommandText = await File.ReadAllTextAsync(dataPath);
            await command.ExecuteNonQueryAsync();
        }
    }
}
