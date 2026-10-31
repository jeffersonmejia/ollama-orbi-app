namespace OrbiApp.Application.Features.Delivery.Ports;

public interface IMarketPriceProvider
{
    Task<IReadOnlyList<MarketPriceSource>> FindPricesAsync(string productName, CancellationToken cancellationToken);
}

public sealed record MarketPriceSource(string Store, string Product, decimal Price, string Url);
public sealed record MarketPriceAnalysis(decimal SuggestedPrice, decimal MinimumPrice, decimal MaximumPrice, IReadOnlyList<MarketPriceSource> Sources);
