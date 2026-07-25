namespace SakilaApp.Models.Delivery;

public class DeliveryProduct
{
    public int DeliveryProductId { get; set; }
    public int DeliveryStoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DeliveryStore Store { get; set; } = null!;
}
