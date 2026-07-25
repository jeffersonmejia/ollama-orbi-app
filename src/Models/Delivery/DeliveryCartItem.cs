namespace SakilaApp.Models.Delivery;

public class DeliveryCartItem
{
    public long DeliveryCartItemId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public int DeliveryProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DeliveryProduct Product { get; set; } = null!;
}
