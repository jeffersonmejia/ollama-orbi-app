namespace SakilaApp.Models.Delivery;

public class DeliveryOrder
{
    public int DeliveryOrderId { get; set; }
    public int DeliveryStoreId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string? DeliveryPersonEmail { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public string Status { get; set; } = "Pendiente";
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DeliveryStore Store { get; set; } = null!;
    public ICollection<DeliveryOrderItem> Items { get; set; } = new List<DeliveryOrderItem>();
}
