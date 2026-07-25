using Microsoft.AspNetCore.Identity;
using SakilaApp.Models.Delivery;

namespace SakilaApp.Models.Operations;

public class StockReservation
{
    public long StockReservationId { get; set; }
    public int DeliveryProductId { get; set; }
    public int DeliveryOrderId { get; set; }
    public string? ReservedByUserId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = "Activa";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedAt { get; set; }

    public DeliveryProduct Product { get; set; } = null!;
    public DeliveryOrder Order { get; set; } = null!;
    public IdentityUser? ReservedByUser { get; set; }
}
