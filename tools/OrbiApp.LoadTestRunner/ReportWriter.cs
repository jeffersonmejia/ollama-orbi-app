using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OrbiApp.LoadTestRunner;

public sealed class ReportWriter(string repositoryRoot)
{
    public async Task SaveRunAsync(RunMetadata metadata, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(repositoryRoot, "tests", "k6", "results", metadata.RunId);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "metadata.json"),
            JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        await RefreshComparisonAsync(cancellationToken);
    }

    public async Task RefreshComparisonAsync(CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(repositoryRoot, "tests", "k6", "results");
        Directory.CreateDirectory(root);
        var records = new List<(RunMetadata Metadata, RunMetrics? Metrics)>();
        foreach (var metadataFile in Directory.EnumerateFiles(root, "metadata.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var metadata = JsonSerializer.Deserialize<RunMetadata>(await File.ReadAllTextAsync(metadataFile, cancellationToken));
                if (metadata is null)
                    continue;
                var summaryPath = Path.Combine(Path.GetDirectoryName(metadataFile)!, "k6-summary.json");
                var metrics = File.Exists(summaryPath) ? K6Executor.ParseMetrics(summaryPath) : null;
                records.Add((metadata, metrics));
            }
            catch (JsonException)
            {
                // A corrupt or partial result remains in its run directory and is not represented as a valid measurement.
            }
            catch (IOException)
            {
                // A partially written result is omitted until the next report.
            }
        }

        var rows = new List<ComparisonRow>();
        foreach (var replicas in new[] { 1, 3, 10 })
        foreach (var vus in new[] { 20, 50, 100, 200, 500 })
        {
            var result = records
                .Where(item => item.Metadata.Profile is "scalability" or "matrix" &&
                    item.Metadata.ReplicasRequested == replicas && item.Metadata.Vus == vus)
                .OrderByDescending(item => item.Metadata.StartedAtUtc)
                .FirstOrDefault();
            rows.Add(result.Metadata is null
                ? new ComparisonRow("", "", "", "", replicas, 0, vus, null, 30, null, null, null, null, null, null, null, null, "", "NO_EJECUTADO")
                : new ComparisonRow(result.Metadata.RunId,
                    result.Metadata.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    result.Metadata.GitCommit, result.Metadata.AppImage,
                    replicas, result.Metadata.ReplicasReady, vus, result.Metadata.VusObserved,
                    result.Metadata.DurationSeconds, result.Metadata.DurationObservedSeconds,
                    result.Metrics?.TotalRequests, result.Metrics?.RequestsPerSecond, result.Metrics?.ErrorRate,
                    result.Metrics?.AverageMilliseconds, result.Metrics?.P95Milliseconds, result.Metrics?.P99Milliseconds,
                    result.Metrics?.ChecksRate, result.Metadata.ThresholdStatus, result.Metadata.RunStatus));
        }

        await File.WriteAllTextAsync(Path.Combine(root, "comparison.csv"), WriteCsv(rows), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, "comparison.md"), WriteMarkdown(rows, records.Count), cancellationToken);
    }

    private static string WriteCsv(IEnumerable<ComparisonRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("run_id,started_at_utc,git_commit,app_image,replicas_requested,replicas_ready,vus_requested,vus_observed,duration_requested_s,duration_observed_s,total_requests,rps,error_rate,avg_ms,p95_ms,p99_ms,checks_rate,threshold_status,run_status");
        foreach (var row in rows)
        {
            builder.AppendJoin(',',
                Csv(row.RunId), Csv(row.StartedAtUtc), Csv(row.GitCommit), Csv(row.AppImage),
                row.ReplicasRequested, row.ReplicasReady, row.Vus, Number(row.VusObserved),
                row.DurationSeconds, Number(row.DurationObservedSeconds),
                Number(row.TotalRequests), Number(row.RequestsPerSecond), Number(row.ErrorRate),
                Number(row.AverageMilliseconds), Number(row.P95Milliseconds), Number(row.P99Milliseconds), Number(row.ChecksRate),
                Csv(row.ThresholdStatus), Csv(row.RunStatus));
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string WriteMarkdown(IReadOnlyCollection<ComparisonRow> rows, int observedRuns)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Orbi k6: comparación de escenarios");
        builder.AppendLine();
        builder.AppendLine($"Corridas con metadatos leídas: {observedRuns}. Los campos sin datos se mantienen vacíos; no se estiman resultados.");
        builder.AppendLine();
        builder.AppendLine("| Réplicas | VUs solicitados | VUs observados | Estado | Ready | RPS | Error | Media ms | p95 ms | p99 ms | Run ID |");
        builder.AppendLine("|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---|");
        foreach (var row in rows)
            builder.AppendLine($"| {row.ReplicasRequested} | {row.Vus} | {Number(row.VusObserved)} | {row.RunStatus} | {row.ReplicasReady} | {Number(row.RequestsPerSecond)} | {Number(row.ErrorRate)} | {Number(row.AverageMilliseconds)} | {Number(row.P95Milliseconds)} | {Number(row.P99Milliseconds)} | {row.RunId} |");
        builder.AppendLine();
        builder.AppendLine("## Interpretación");
        builder.AppendLine();
        builder.AppendLine("RPS y latencias describen el camino HTTP observado por k6. Para atribuir degradación a k6, red, backend o PostgreSQL, correlaciona cada corrida con el estado/recursos Kubernetes, el host generador y el tiempo de readiness guardados en su carpeta.");
        return builder.ToString();
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string Number(double? value) => value?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;

    private sealed record ComparisonRow(
        string RunId,
        string StartedAtUtc,
        string GitCommit,
        string AppImage,
        int ReplicasRequested,
        int ReplicasReady,
        int Vus,
        double? VusObserved,
        int DurationSeconds,
        double? DurationObservedSeconds,
        double? TotalRequests,
        double? RequestsPerSecond,
        double? ErrorRate,
        double? AverageMilliseconds,
        double? P95Milliseconds,
        double? P99Milliseconds,
        double? ChecksRate,
        string ThresholdStatus,
        string RunStatus);
}
