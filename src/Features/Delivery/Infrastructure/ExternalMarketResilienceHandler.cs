using System.Net;

namespace OrbiApp.Infrastructure.Features.Delivery;

public sealed class ExternalMarketCircuitState
{
    private readonly object _gate = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _openUntil;

    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                if (_openUntil is null) return false;
                if (_openUntil > DateTimeOffset.UtcNow) return true;
                _openUntil = null;
                _consecutiveFailures = 0;
                return false;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openUntil = null;
        }
    }

    public bool RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures < 5) return false;
            _openUntil = DateTimeOffset.UtcNow.AddSeconds(30);
            return true;
        }
    }
}

public sealed class ExternalMarketResilienceHandler(
    ExternalMarketCircuitState circuit,
    ILogger<ExternalMarketResilienceHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get)
            return await base.SendAsync(request, cancellationToken);

        if (circuit.IsOpen)
            throw new HttpRequestException("External market data is temporarily unavailable.");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (IsTransient(response.StatusCode))
                {
                    var opened = circuit.RecordFailure();
                    if (opened)
                        logger.LogWarning("External market circuit opened. DurationSeconds={DurationSeconds}", 30);

                    if (attempt < 2)
                    {
                        response.Dispose();
                        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                        continue;
                    }

                    return response;
                }

                if (response.IsSuccessStatusCode)
                    circuit.RecordSuccess();
                return response;
            }
            catch (HttpRequestException) when (attempt < 2 && !cancellationToken.IsCancellationRequested)
            {
                var opened = circuit.RecordFailure();
                if (opened)
                    logger.LogWarning("External market circuit opened. DurationSeconds={DurationSeconds}", 30);
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            catch (HttpRequestException)
            {
                circuit.RecordFailure();
                throw;
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout || (int)statusCode >= 500;
}
