using System.Text.Json;

namespace OrbiApp.LoadTestRunner;

public sealed class PrometheusClient(HttpClient? client = null)
{
    private readonly HttpClient _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Uri _baseUri = new(Environment.GetEnvironmentVariable("ORBI_LAB_PROMETHEUS_URL") ?? "http://127.0.0.1:19090/");

    public async Task WaitForRunRequestsAsync(string runId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(runId, "^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$"))
            throw new ArgumentException("Run ID no válido.", nameof(runId));
        var query = $"k6_http_reqs_total{{testid=\"{runId}\"}}";
        var uri = new Uri(_baseUri, $"api/v1/query?query={Uri.EscapeDataString(query)}");
        var expiresAt = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < expiresAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var response = await _client.GetAsync(uri, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    var data = json.RootElement.GetProperty("data");
                    if (data.TryGetProperty("result", out var values) && values.GetArrayLength() > 0)
                    {
                        var sample = values[0].GetProperty("value");
                        if (sample.GetArrayLength() > 1 &&
                            double.TryParse(sample[1].GetString(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var count) && count > 0)
                            return;
                    }
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        throw new TimeoutException("No llegaron muestras k6 a Prometheus; no se inicia una perturbación del clúster.");
    }
}
