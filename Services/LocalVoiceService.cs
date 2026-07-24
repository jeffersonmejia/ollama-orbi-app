using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SakilaApp.Services;

public sealed class LocalVoiceService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public LocalVoiceService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        using var audioContent = new StreamContent(audio);
        audioContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentType, out var parsedContentType)
            ? parsedContentType
            : new System.Net.Http.Headers.MediaTypeHeaderValue("audio/webm");
        content.Add(audioContent, "file", string.IsNullOrWhiteSpace(fileName) ? "voice.webm" : fileName);
        content.Add(new StringContent(_configuration["Voice:TranscriptionModel"] ?? "Systran/faster-whisper-small"), "model");
        content.Add(new StringContent("es"), "language");
        content.Add(new StringContent("json"), "response_format");

        using var response = await _httpClient.PostAsync("v1/audio/transcriptions", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(cancellationToken);
        return result?.Text?.Trim() ?? string.Empty;
    }

    public async Task<(byte[] Audio, string ContentType)> SynthesizeAsync(string text, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync("v1/audio/speech", new
        {
            model = _configuration["Voice:SpeechModel"] ?? "speaches-ai/Kokoro-82M-v1.0-ONNX",
            voice = _configuration["Voice:Voice"] ?? "ef_dora",
            input = text,
            response_format = "mp3",
            speed = 0.95
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsByteArrayAsync(cancellationToken),
            response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg");
    }

    private sealed class TranscriptionResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
