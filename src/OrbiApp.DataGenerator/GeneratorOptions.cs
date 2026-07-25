using System.Globalization;
using System.Text.Json;

namespace OrbiApp.DataGenerator;

internal sealed record GeneratorOptions(
    string ConnectionString,
    int TotalRecords,
    int BatchSize,
    int Seed,
    string Locale,
    DateTimeOffset ReferenceDate,
    bool Reset,
    bool OnlyProducts,
    string SchemaDirectory)
{
    public static GeneratorOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Argumento no reconocido: {args[i]}");

            var key = args[i][2..];
            if (key is "reset" or "help" or "only-products")
            {
                flags.Add(key);
                continue;
            }

            if (++i >= args.Length)
                throw new ArgumentException($"Falta el valor de --{key}.");
            values[key] = args[i];
        }

        if (flags.Contains("help"))
            throw new HelpRequestedException();

        var schemaDirectory = ResolveSchemaDirectory(values.GetValueOrDefault("schema-dir"));
        var appSettings = ReadAppSettings(Path.Combine(schemaDirectory, "..", "appsettings.json"));
        var section = appSettings.DataGeneration ?? new DataGenerationSettings();

        var connection = values.GetValueOrDefault("connection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? appSettings.ConnectionStrings?.GetValueOrDefault("DefaultConnection")
            ?? throw new ArgumentException("Configure --connection o ConnectionStrings__DefaultConnection.");

        var options = new GeneratorOptions(
            connection,
            ReadInt("records", "DATA_GENERATION_RECORDS", section.TotalRecords, values),
            ReadInt("batch-size", "DATA_GENERATION_BATCH_SIZE", section.BatchSize, values),
            ReadInt("seed", "DATA_GENERATION_SEED", section.Seed, values),
            values.GetValueOrDefault("locale") ?? Environment.GetEnvironmentVariable("DATA_GENERATION_LOCALE") ?? section.Locale,
            DateTimeOffset.Parse(values.GetValueOrDefault("reference-date") ?? section.ReferenceDate, CultureInfo.InvariantCulture),
            flags.Contains("reset"),
            flags.Contains("only-products"),
            schemaDirectory);

        if (options.TotalRecords < 1_000)
            throw new ArgumentOutOfRangeException("--records", "Se requieren al menos 1.000 registros para conservar todas las relaciones.");
        if (options.BatchSize is < 100 or > 50_000)
            throw new ArgumentOutOfRangeException("--batch-size", "Debe estar entre 100 y 50.000.");
        return options;
    }

    public static void PrintHelp() => Console.WriteLine("""
        Generador Bogus de Orbi App

        dotnet run --project src/OrbiApp.DataGenerator -- [opciones]

          --records N           Total exacto de registros (predeterminado: 1000000)
          --batch-size N        Tamaño de lote (predeterminado: 5000)
          --seed N              Semilla reproducible (predeterminado: 2026)
          --locale es           Locale de Bogus
          --reference-date ISO  Fecha ancla reproducible
          --connection VALUE    Conexión PostgreSQL; se recomienda la variable
                                ConnectionStrings__DefaultConnection
          --schema-dir PATH     Directorio que contiene orbi-schema.sql
          --reset               Vacía las tablas de negocio antes de generar
          --only-products       Solo genera productos en tiendas existentes
          --help                Muestra esta ayuda

        Sin --reset el comando se niega a escribir en una base con datos de negocio.
        """);

    private static int ReadInt(string argument, string environment, int fallback, IReadOnlyDictionary<string, string> values) =>
        int.Parse(values.GetValueOrDefault(argument) ?? Environment.GetEnvironmentVariable(environment) ?? fallback.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static string ResolveSchemaDirectory(string? requested)
    {
        var candidates = new[]
        {
            requested,
            Path.Combine(Environment.CurrentDirectory, "db"),
            Path.Combine(Environment.CurrentDirectory, "src", "db"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "db"))
        };
        return candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Path.GetFullPath)
            .FirstOrDefault(x => File.Exists(Path.Combine(x, "orbi-schema.sql")))
            ?? throw new DirectoryNotFoundException("No se encontró src/db/orbi-schema.sql. Use --schema-dir.");
    }

    private static RootSettings ReadAppSettings(string path)
    {
        if (!File.Exists(path)) return new RootSettings();
        return JsonSerializer.Deserialize<RootSettings>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new RootSettings();
    }

    private sealed class RootSettings
    {
        public Dictionary<string, string>? ConnectionStrings { get; init; }
        public DataGenerationSettings? DataGeneration { get; init; }
    }

    private sealed class DataGenerationSettings
    {
        public int TotalRecords { get; init; } = 1_000_000;
        public int BatchSize { get; init; } = 5_000;
        public int Seed { get; init; } = 2026;
        public string Locale { get; init; } = "es";
        public string ReferenceDate { get; init; } = "2026-07-01T00:00:00Z";
    }
}

internal sealed class HelpRequestedException : Exception;
