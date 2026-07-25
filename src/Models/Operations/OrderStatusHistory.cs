using Microsoft.AspNetCore.Identity;
using SakilaApp.Models.Delivery;

namespace SakilaApp.Models.Operations;

public class OrderStatusHistory
{
    public long OrderStatusHistoryId { get; set; }
    public int DeliveryOrderId { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public DeliveryOrder Order { get; set; } = null!;
    public IdentityUser? ChangedByUser { get; set; }
}
