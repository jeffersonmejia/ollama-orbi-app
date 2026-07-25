using Microsoft.AspNetCore.Identity;
using SakilaApp.Models.Delivery;

namespace SakilaApp.Models.Operations;

public class InventoryMovement
{
    public long InventoryMovementId { get; set; }
    public int DeliveryProductId { get; set; }
    public int? DeliveryOrderId { get; set; }
    public string? PerformedByUserId { get; set; }
    public string MovementType { get; set; } = "Ajuste";
    public int QuantityDelta { get; set; }
    public decimal? UnitCost { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DeliveryProduct Product { get; set; } = null!;
    public DeliveryOrder? Order { get; set; }
    public IdentityUser? PerformedByUser { get; set; }
}
