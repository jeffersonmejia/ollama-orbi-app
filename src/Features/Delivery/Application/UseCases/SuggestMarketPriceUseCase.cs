using OrbiApp.Application.Features.Delivery.Ports;

namespace OrbiApp.Application.Features.Delivery.UseCases;

/// <summary>Applies Dash's pricing rule to prices returned by the external commerce adapter.</summary>
public sealed class SuggestMarketPriceUseCase(IMarketPriceProvider priceProvider) : ISuggestMarketPriceUseCase
{
    public async Task<MarketPriceAnalysis?> ExecuteAsync(string productName, CancellationToken cancellationToken)
    {
        var sources = await priceProvider.FindPricesAsync(productName, cancellationToken);
        if (sources.Count == 0) return null;

        var prices = sources.Select(source => source.Price).Order().ToArray();
        var middle = prices.Length / 2;
        var median = prices.Length % 2 == 0
            ? (prices[middle - 1] + prices[middle]) / 2m
            : prices[middle];

        return new MarketPriceAnalysis(
            decimal.Round(median, 2, MidpointRounding.AwayFromZero),
            prices[0],
            prices[^1],
            sources);
    }
}
