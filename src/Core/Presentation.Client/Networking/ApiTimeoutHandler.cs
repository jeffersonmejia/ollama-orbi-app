using Microsoft.Extensions.Options;

namespace OrbiApp.Presentation.Client.Networking;

public sealed class ApiTimeoutHandler : DelegatingHandler
{
    private readonly TimeSpan _timeout;

    public ApiTimeoutHandler(IOptions<ApiTimeoutOptions> options)
    {
        _timeout = TimeSpan.FromSeconds(Math.Clamp(options.Value.RequestTimeoutSeconds, 1, 300));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(_timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            return await base.SendAsync(request, linkedSource.Token);
        }
        catch (OperationCanceledException exception)
            when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("La solicitud superó el tiempo máximo configurado.", exception, null);
        }
    }
}
