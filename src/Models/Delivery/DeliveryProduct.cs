namespace SakilaApp.Models.Delivery;

public class DeliveryProduct
{
    public int DeliveryProductId { get; set; }
    public int DeliveryStoreId { get; set; }
    public string? CreatedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal UnitCost { get; set; }
    public int Stock { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DeliveryStore Store { get; set; } = null!;
}
