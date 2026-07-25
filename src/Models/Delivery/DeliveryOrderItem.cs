namespace SakilaApp.Models.Delivery;

public class DeliveryOrderItem
{
    public int DeliveryOrderItemId { get; set; }
    public int DeliveryOrderId { get; set; }
    public int DeliveryProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public DeliveryOrder Order { get; set; } = null!;
}
