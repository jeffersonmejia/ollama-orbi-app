using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SakilaApp.Services;

public sealed class ExternalMarketPriceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalMarketPriceService> _logger;

    public ExternalMarketPriceService(HttpClient httpClient, ILogger<ExternalMarketPriceService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<MarketPriceAnalysis?> AnalyzeAsync(string productName, CancellationToken cancellationToken)
    {
        var query = NormalizeQuery(productName);
        if (string.IsNullOrWhiteSpace(query)) return null;

        var tasks = new[]
        {
            ReadFybecaAsync(query, cancellationToken),
            ReadVtexAsync("Pharmacys", "https://www.pharmacys.com.ec", query, cancellationToken),
            ReadVtexAsync("Farmacias Medicity", "https://www.farmaciasmedicity.com", query, cancellationToken)
        };
        var results = await Task.WhenAll(tasks);
        var sources = results.Where(result => result is not null).Cast<MarketPriceSource>().ToList();
        if (sources.Count == 0) return null;

        var orderedPrices = sources.Select(source => source.Price).OrderBy(price => price).ToArray();
        var middle = orderedPrices.Length / 2;
        var median = orderedPrices.Length % 2 == 0
            ? (orderedPrices[middle - 1] + orderedPrices[middle]) / 2m
            : orderedPrices[middle];

        return new MarketPriceAnalysis(
            decimal.Round(median, 2, MidpointRounding.AwayFromZero),
            orderedPrices[0],
            orderedPrices[^1],
            sources);
    }

    private async Task<MarketPriceSource?> ReadFybecaAsync(string query, CancellationToken cancellationToken)
    {
        var url = "https://www.fybeca.com/on/demandware.store/Sites-FybecaEcuador-Site/es_EC/" +
            $"Search-UpdateGrid?q={Uri.EscapeDataString(query)}&start=0&sz=12";
        try
        {
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            var cards = Regex.Matches(
                html,
                "<div class=\"product product-wrapper\".*?&quot;name&quot;:&quot;(?<name>.*?)&quot;.*?" +
                "<a class=\"link\" href=\"(?<url>[^\"]+)\">.*?<div class=\"price\">.*?" +
                "<span class=\"value\" content=\"(?<price>\\d+(?:\\.\\d+)?)\">",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return SelectBestMatch("Fybeca", "https://www.fybeca.com", query, cards.Select(match =>
                new MarketPriceSource(
                    "Fybeca",
                    WebUtility.HtmlDecode(match.Groups["name"].Value),
                    decimal.Parse(match.Groups["price"].Value, CultureInfo.InvariantCulture),
                    new Uri(new Uri("https://www.fybeca.com"), match.Groups["url"].Value).ToString())));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "No se pudo consultar Fybeca para {Query}", query);
            return null;
        }
    }

    private async Task<MarketPriceSource?> ReadVtexAsync(
        string store,
        string baseUrl,
        string query,
        CancellationToken cancellationToken)
    {
        var slug = Regex.Replace(query.ToLowerInvariant(), @"[^a-z0-9áéíóúñ]+", "-").Trim('-');
        var url = $"{baseUrl}/{Uri.EscapeDataString(slug)}?_q={Uri.EscapeDataString(query)}&map=ft";
        try
        {
            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            var script = Regex.Matches(
                    html,
                    "<script[^>]*type=\"application/ld\\+json\"[^>]*>(?<json>.*?)</script>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline)
                .Select(match => match.Groups["json"].Value)
                .FirstOrDefault(json => json.Contains("\"ItemList\"", StringComparison.Ordinal));
            if (script is null) return null;

            using var document = JsonDocument.Parse(WebUtility.HtmlDecode(script));
            if (!document.RootElement.TryGetProperty("itemListElement", out var elements)) return null;
            var candidates = new List<MarketPriceSource>();
            foreach (var element in elements.EnumerateArray())
            {
                if (!element.TryGetProperty("item", out var item) ||
                    !item.TryGetProperty("name", out var nameElement) ||
                    !item.TryGetProperty("offers", out var offers)) continue;
                var name = nameElement.GetString();
                var productUrl = item.TryGetProperty("@id", out var idElement) ? idElement.GetString() : url;
                if (string.IsNullOrWhiteSpace(name) || !TryReadPrice(offers, out var price) || price <= 0) continue;
                candidates.Add(new MarketPriceSource(store, name, price, productUrl ?? url));
            }
            return SelectBestMatch(store, baseUrl, query, candidates);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "No se pudo consultar {Store} para {Query}", store, query);
            return null;
        }
    }

    private static bool TryReadPrice(JsonElement offers, out decimal price)
    {
        price = 0;
        if (offers.TryGetProperty("lowPrice", out var lowPrice) && lowPrice.TryGetDecimal(out price)) return true;
        return offers.TryGetProperty("price", out var directPrice) && directPrice.TryGetDecimal(out price);
    }

    private static MarketPriceSource? SelectBestMatch(
        string store,
        string baseUrl,
        string query,
        IEnumerable<MarketPriceSource> candidates)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 4)
            .ToArray();
        return candidates
            .Where(candidate => candidate.Price is > 0 and < 500)
            .OrderByDescending(candidate => terms.Count(term =>
                candidate.Product.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(candidate => candidate.Price)
            .Select(candidate => candidate with
            {
                Store = store,
                Url = Uri.TryCreate(candidate.Url, UriKind.Absolute, out _)
                    ? candidate.Url
                    : new Uri(new Uri(baseUrl), candidate.Url).ToString()
            })
            .FirstOrDefault();
    }

    private static string NormalizeQuery(string productName)
    {
        var normalized = Regex.Replace(productName.Trim(), @"\s+", " ");
        return Regex.Replace(normalized, "acitromicina", "azitromicina", RegexOptions.IgnoreCase);
    }
}

public sealed record MarketPriceSource(string Store, string Product, decimal Price, string Url);

public sealed record MarketPriceAnalysis(
    decimal SuggestedPrice,
    decimal MinimumPrice,
    decimal MaximumPrice,
    IReadOnlyList<MarketPriceSource> Sources);
