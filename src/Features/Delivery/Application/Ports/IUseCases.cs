namespace OrbiApp.Application.Features.Delivery.Ports;

/// <summary>Inbound port used by the presentation adapter to request a market-price analysis.</summary>
public interface ISuggestMarketPriceUseCase
{
    Task<MarketPriceAnalysis?> ExecuteAsync(string productName, CancellationToken cancellationToken);
}
