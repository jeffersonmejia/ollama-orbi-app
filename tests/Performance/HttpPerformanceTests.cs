using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace OrbiApp.Performance.Tests;

public sealed class HttpPerformanceTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public async Task Http_endpoints_return_expected_data_and_record_latency_percentiles()
    {
        if (Environment.GetEnvironmentVariable("ORBI_PERFORMANCE_ENABLED") != "1")
            throw Xunit.Sdk.SkipException.ForSkip("Set ORBI_PERFORMANCE_ENABLED=1 and ORBI_DATABASE_TEST_CONNECTION to run isolated HTTP performance tests.");
        await using var host = new HttpTestHost();
        await host.StartAsync();
        var scenarios = new (string Name, string Path, Func<JsonDocument, bool> Validate)[]
        {
            ("catalog existing paginated", host.CatalogPath, json =>
                json.RootElement.GetProperty("products").GetArrayLength() == 12 && json.RootElement.GetProperty("totalCount").GetInt32() == 40_000),
            ("orders existing page 1", host.OrdersPath, json =>
                json.RootElement.GetProperty("orders").GetArrayLength() == 5 && json.RootElement.GetProperty("totalCount").GetInt32() == 13 && json.RootElement.GetProperty("hasNextPage").GetBoolean()),
            ("orders existing page 2", host.OrdersPageTwoPath, json =>
                json.RootElement.GetProperty("orders").GetArrayLength() == 5 && json.RootElement.GetProperty("pageIndex").GetInt32() == 2),
            ("orders empty", host.EmptyOrdersPath, json =>
                json.RootElement.GetProperty("orders").GetArrayLength() == 0 && json.RootElement.GetProperty("totalCount").GetInt32() == 0),
            ("payments existing page 1", host.PaymentsPath, json =>
                json.RootElement.GetProperty("payments").GetArrayLength() == 12 && json.RootElement.GetProperty("totalCount").GetInt32() == 13 && json.RootElement.GetProperty("hasNextPage").GetBoolean()),
            ("payments existing page 2", host.PaymentsPageTwoPath, json =>
                json.RootElement.GetProperty("payments").GetArrayLength() == 1 && json.RootElement.GetProperty("pageIndex").GetInt32() == 2),
            ("payments empty", host.EmptyPaymentsPath, json =>
                json.RootElement.GetProperty("payments").GetArrayLength() == 0 && json.RootElement.GetProperty("totalCount").GetInt32() == 0)
        };

        foreach (var scenario in scenarios)
        {
            for (var warmup = 0; warmup < 3; warmup++) await RequestAndValidateAsync(host.Client, scenario.Path, scenario.Validate);
            var samples = new double[30];
            for (var index = 0; index < samples.Length; index++)
            {
                var timer = Stopwatch.StartNew();
                await RequestAndValidateAsync(host.Client, scenario.Path, scenario.Validate);
                samples[index] = timer.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            var stats = new PerformanceStats(samples[0], samples.Average(), Percentile(samples, .50),
                Percentile(samples, .95), Percentile(samples, .99), samples[^1], samples.Length);
            output.WriteLine($"HTTP {scenario.Name}: min={stats.MinMs:F2} avg={stats.AverageMs:F2} p50={stats.P50Ms:F2} p95={stats.P95Ms:F2} p99={stats.P99Ms:F2} max={stats.MaxMs:F2} ms");
            await PerformanceReport.WriteAsync("http", scenario.Name, stats);
        }

        for (var warmup = 0; warmup < 3; warmup++) await RequestOperationsAsync(host.Client);
        var operationsSamples = new double[30];
        for (var index = 0; index < operationsSamples.Length; index++)
        {
            var timer = Stopwatch.StartNew();
            await RequestOperationsAsync(host.Client);
            operationsSamples[index] = timer.Elapsed.TotalMilliseconds;
        }
        Array.Sort(operationsSamples);
        await PerformanceReport.WriteAsync("http", "operations inventory movements", new PerformanceStats(
            operationsSamples[0], operationsSamples.Average(), Percentile(operationsSamples, .50),
            Percentile(operationsSamples, .95), Percentile(operationsSamples, .99), operationsSamples[^1], operationsSamples.Length));
    }

    private static async Task RequestAndValidateAsync(HttpClient client, string path, Func<JsonDocument, bool> validate)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        Assert.True(validate(json), $"Unexpected response body: {json.RootElement}");
    }

    private static async Task RequestOperationsAsync(HttpClient client)
    {
        using var response = await client.GetAsync(HttpTestHost.OperationsPath);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Movimientos de inventario", body, StringComparison.OrdinalIgnoreCase);
    }

    private static double Percentile(double[] sorted, double percentile) =>
        sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];
}
