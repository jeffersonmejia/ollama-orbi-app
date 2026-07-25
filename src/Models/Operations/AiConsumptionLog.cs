using Microsoft.AspNetCore.Identity;

namespace SakilaApp.Models.Operations;

public class AiConsumptionLog
{
    public long AiConsumptionLogId { get; set; }
    public string? UserId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string PromptText { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCost { get; set; }
    public int DurationMilliseconds { get; set; }
    public string? IpAddress { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public IdentityUser? User { get; set; }
}
