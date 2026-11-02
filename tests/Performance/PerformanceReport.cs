using System.Text.Json;
using System.Text;

namespace OrbiApp.Performance.Tests;

internal sealed record PerformanceStats(double MinMs, double AverageMs, double P50Ms, double P95Ms,
    double P99Ms, double MaxMs, int Samples, double? RequestsPerSecond = null,
    double? ErrorPercent = null, int? Concurrency = null, Dictionary<string, int>? StatusCodes = null);

internal static class PerformanceReport
{
    public static async Task WriteAsync(string suite, string scenario, PerformanceStats stats)
    {
        var directory = Path.Combine(FindRepositoryRoot(), "tests", "Performance", "Reports");
        Directory.CreateDirectory(directory);
        var jsonPath = Path.Combine(directory, "performance-results.json");
        var markdownPath = Path.Combine(directory, "performance-results.md");
        var data = new Dictionary<string, Dictionary<string, PerformanceStats>>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(jsonPath))
        {
            await using var input = File.OpenRead(jsonPath);
            data = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, PerformanceStats>>>(input) ?? data;
        }
        if (!data.TryGetValue(suite, out var scenarios)) data[suite] = scenarios = new();
        scenarios[scenario] = stats;
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        var markdown = new StringBuilder("# Resultados de rendimiento Orbi\n\nLatencias en milisegundos. Los resultados aparecen solo después de una ejecución real.\n\n");
        foreach (var (suiteName, suiteScenarios) in data.OrderBy(item => item.Key))
        {
            markdown.AppendLine($"## {suiteName}").AppendLine();
            markdown.AppendLine("| Escenario | Usuarios | Min | Promedio | P50 | P95 | P99 | Máx | Req/s | Errores % | HTTP |");
            markdown.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
            foreach (var (name, result) in suiteScenarios.OrderBy(item => item.Key))
            {
                var codes = result.StatusCodes is null ? "—" : string.Join(", ", result.StatusCodes.OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Value}"));
                markdown.AppendLine($"| {name} | {result.Concurrency?.ToString() ?? "—"} | {result.MinMs:F2} | {result.AverageMs:F2} | {result.P50Ms:F2} | {result.P95Ms:F2} | {result.P99Ms:F2} | {result.MaxMs:F2} | {result.RequestsPerSecond?.ToString("F2") ?? "—"} | {result.ErrorPercent?.ToString("F2") ?? "—"} | {codes} |");
            }
            markdown.AppendLine();
        }
        await File.WriteAllTextAsync(markdownPath, markdown.ToString());
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("ORBI_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot) && Directory.Exists(Path.Combine(configuredRoot, "tests")) && Directory.Exists(Path.Combine(configuredRoot, "src")))
            return configuredRoot;
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (Directory.Exists(Path.Combine(directory.FullName, "tests")) && Directory.Exists(Path.Combine(directory.FullName, "src")))
                    return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate repository root for performance artifacts.");
    }
}
