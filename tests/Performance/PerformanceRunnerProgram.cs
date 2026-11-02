using System.Collections.Concurrent;
using System.Diagnostics;

namespace OrbiApp.Performance.Tests;

internal static class PerformanceRunnerProgram
{
    private static readonly (string Name, Func<HttpTestHost, string> Path)[] Scenarios =
    [
        ("catalog", host => host.CatalogPath),
        ("orders", host => host.OrdersPath),
        ("payments", host => host.PaymentsPath),
        ("operations", _ => HttpTestHost.OperationsPath)
    ];

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || args[0] is not ("load" or "stress"))
        {
            Console.Error.WriteLine("Usage: dotnet run --project tests/Performance/OrbiApp.Performance.Tests.csproj -- load|stress");
            return 2;
        }
        var mode = args[0];
        if (Environment.GetEnvironmentVariable("ORBI_LOAD_TEST_ENABLED") != "1")
        {
            Console.Error.WriteLine("Refusing to run. Set ORBI_LOAD_TEST_ENABLED=1 after verifying the isolated PostgreSQL test service.");
            return 2;
        }

        var durationSeconds = ReadBoundedInt("ORBI_LOAD_DURATION_SECONDS", 30, 1, 300);
        var maxRequests = ReadBoundedInt("ORBI_LOAD_MAX_REQUESTS", 100_000, 1, 250_000);
        double? maxP95 = null;
        double? maxErrorPercent = null;
        if (mode == "stress")
        {
            maxP95 = ReadRequiredDouble("ORBI_STRESS_MAX_P95_MS", 0.1, 600_000);
            maxErrorPercent = ReadRequiredDouble("ORBI_STRESS_MAX_ERROR_PERCENT", 0, 100);
        }

        await using var host = new HttpTestHost();
        await host.StartAsync(); // migrations, seed data, and app startup are outside all timed windows
        foreach (var scenario in Scenarios)
        {
            using var warmup = await host.Client.GetAsync(scenario.Path(host));
            if (!warmup.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Warmup failed for {scenario.Name}: HTTP {(int)warmup.StatusCode}; stopping.");
                return 1;
            }
        }

        var levels = mode == "load" ? new[] { 1, 10, 25, 50 } : new[] { 10, 25, 50, 100, 200 };
        foreach (var concurrency in levels)
        {
            var result = await RunScenarioAsync(host, concurrency, TimeSpan.FromSeconds(durationSeconds), maxRequests,
                stopOnPersistentErrors: mode == "stress");
            await PerformanceReport.WriteAsync(mode, $"{concurrency} virtual users / {durationSeconds}s", result.Stats);
            Console.WriteLine($"{mode} {concurrency} users: {result.Stats.RequestsPerSecond:F2} req/s, p95={result.Stats.P95Ms:F2} ms, p99={result.Stats.P99Ms:F2} ms, errors={result.Stats.ErrorPercent:F2}%, http={FormatCodes(result.Stats.StatusCodes)}");
            if (mode == "stress" && (result.PersistentFailure || result.Stats.P95Ms > maxP95!.Value || result.Stats.ErrorPercent > maxErrorPercent!.Value))
            {
                Console.WriteLine("Stress degradation threshold crossed; stopping further concurrency increases.");
                break;
            }
        }

        if (mode == "stress")
        {
            var recoverySeconds = ReadBoundedInt("ORBI_STRESS_RECOVERY_SECONDS", 10, 1, 120);
            var recovery = await RunScenarioAsync(host, 1, TimeSpan.FromSeconds(recoverySeconds),
                Math.Min(maxRequests, 10_000), stopOnPersistentErrors: false);
            await PerformanceReport.WriteAsync("stress", $"recovery / {recoverySeconds}s", recovery.Stats);
            Console.WriteLine($"recovery: {recovery.Stats.RequestsPerSecond:F2} req/s, p95={recovery.Stats.P95Ms:F2} ms, errors={recovery.Stats.ErrorPercent:F2}%");
        }
        return 0;
    }

    private static async Task<(PerformanceStats Stats, bool PersistentFailure)> RunScenarioAsync(
        HttpTestHost host, int concurrency, TimeSpan duration, int requestLimit, bool stopOnPersistentErrors)
    {
        var samples = new ConcurrentBag<(double Ms, int Status)>();
        using var stop = new CancellationTokenSource();
        var window = Stopwatch.StartNew();
        var reserved = 0;
        var failuresInARow = 0;
        var persistentFailure = false;
        var workers = Enumerable.Range(0, concurrency).Select(async worker =>
        {
            while (!stop.IsCancellationRequested)
            {
                var reservation = Interlocked.Increment(ref reserved);
                if (reservation > requestLimit) break;
                var scenario = Scenarios[(reservation + worker) % Scenarios.Length];
                var timer = Stopwatch.StartNew();
                var statusCode = 0;
                try
                {
                    using var response = await host.Client.GetAsync(scenario.Path(host));
                    statusCode = (int)response.StatusCode;
                    _ = await response.Content.ReadAsByteArrayAsync();
                    if (response.IsSuccessStatusCode && response.Content.Headers.ContentType is not null)
                        Interlocked.Exchange(ref failuresInARow, 0);
                    else if (Interlocked.Increment(ref failuresInARow) >= 5 && stopOnPersistentErrors)
                    {
                        persistentFailure = true;
                        stop.Cancel();
                    }
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    if (Interlocked.Increment(ref failuresInARow) >= 5 && stopOnPersistentErrors)
                    {
                        persistentFailure = true;
                        stop.Cancel();
                    }
                }
                finally
                {
                    timer.Stop();
                    samples.Add((timer.Elapsed.TotalMilliseconds, statusCode));
                }
            }
        }).ToArray();

        await Task.WhenAny(Task.WhenAll(workers), Task.Delay(duration));
        stop.Cancel();
        await Task.WhenAll(workers);
        window.Stop();
        var ordered = samples.Select(sample => sample.Ms).Order().ToArray();
        var codeCounts = samples.GroupBy(sample => sample.Status)
            .ToDictionary(group => group.Key == 0 ? "error" : group.Key.ToString(), group => group.Count());
        var errorCount = samples.Count(sample => sample.Status is < 200 or >= 400);
        var stats = ordered.Length == 0
            ? new PerformanceStats(0, 0, 0, 0, 0, 0, 0, 0, 100, concurrency, codeCounts)
            : new PerformanceStats(ordered[0], ordered.Average(), Percentile(ordered, .50), Percentile(ordered, .95),
                Percentile(ordered, .99), ordered[^1], ordered.Length, ordered.Length / window.Elapsed.TotalSeconds,
                errorCount * 100d / ordered.Length, concurrency, codeCounts);
        return (stats, persistentFailure);
    }

    private static int ReadBoundedInt(string name, int fallback, int min, int max)
    {
        var text = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        if (!int.TryParse(text, out var value) || value < min || value > max)
            throw new InvalidOperationException($"{name} must be between {min} and {max}.");
        return value;
    }

    private static double ReadRequiredDouble(string name, double min, double max)
    {
        var text = Environment.GetEnvironmentVariable(name);
        if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) || value < min || value > max)
            throw new InvalidOperationException($"Set {name} explicitly to a value between {min} and {max} for stress runs.");
        return value;
    }

    private static double Percentile(double[] sorted, double percentile) =>
        sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];

    private static string FormatCodes(Dictionary<string, int>? counts) => counts is null
        ? "none" : string.Join(", ", counts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
}
