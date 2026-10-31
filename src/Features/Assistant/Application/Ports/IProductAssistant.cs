namespace OrbiApp.Application.Features.Assistant.Ports;

public interface IProductAssistant
{
    Task<OllamaSuggestion> SuggestAsync(string question, string catalog, string assistantContext, CancellationToken cancellationToken);
    IAsyncEnumerable<string> StreamChatAsync(string question, string assistantContext, CancellationToken cancellationToken);
    Task<string> ReviewChatAsync(string question, string assistantContext, string draft, CancellationToken cancellationToken);
}

public sealed record OllamaSuggestion(string Response, string ModelName, int PromptTokens, int CompletionTokens, int DurationMilliseconds);
