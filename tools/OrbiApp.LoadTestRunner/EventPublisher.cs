using System.Net.Http.Json;

namespace OrbiApp.LoadTestRunner;

public sealed class EventPublisher
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(2) };
    private readonly Uri _endpoint;

    public EventPublisher(Uri? endpoint = null) =>
        _endpoint = endpoint ?? new Uri(Environment.GetEnvironmentVariable("ORBI_LAB_EVENTS_URL") ?? "http://127.0.0.1:15190/api/events");

    public async Task PublishAsync(LabEvent message, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Client.PostAsJsonAsync(_endpoint, message, cancellationToken);
            if (!response.IsSuccessStatusCode)
                Console.Error.WriteLine("WARN: el panel de eventos no aceptó una actualización.");
        }
        catch (HttpRequestException)
        {
            Console.Error.WriteLine("WARN: panel de eventos no disponible; continúa la prueba y queda registro local.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("WARN: el panel de eventos tardó demasiado; continúa la prueba.");
        }
    }
}
