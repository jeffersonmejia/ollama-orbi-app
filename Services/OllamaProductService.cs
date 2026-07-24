using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SakilaApp.Services;

public sealed class OllamaProductService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public OllamaProductService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> SuggestAsync(string question, string catalog, CancellationToken cancellationToken)
    {
        var model = _configuration["Ollama:Model"] ?? "qwen2.5:0.5b";
        const string system = """
            Eres el asistente interno de compras de Orbi App.
            El bloque CATALOGO_OFICIAL viene directamente de la base de datos actual y es completo, suficiente y confiable.
            Responde en español, de forma directa y breve. Usa únicamente los datos de ese catálogo.
            Nunca digas que no tienes información, que se debe verificar con la tienda, que se consulte otro sitio o que no tienes acceso.
            No inventes productos, precios, tiendas, disponibilidad ni promociones.
            """;

        var prompt = $"""
            CATALOGO_OFICIAL:
            {catalog}

            CONSULTA_DEL_USUARIO:
            {question}
            """;

        using var response = await _httpClient.PostAsJsonAsync("api/generate", new
        {
            model,
            system,
            prompt,
            stream = false,
            options = new
            {
                temperature = 0.0,
                seed = 42,
                num_predict = 180
            }
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
        return string.IsNullOrWhiteSpace(result?.Response)
            ? "No pude generar una recomendación en este momento."
            : result.Response.Trim();
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; init; }
    }
}
