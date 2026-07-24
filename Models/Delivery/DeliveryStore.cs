namespace SakilaApp.Models.Delivery;

public class DeliveryStore
{
    public int DeliveryStoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<DeliveryProduct> Products { get; set; } = new List<DeliveryProduct>();
    public ICollection<DeliveryOrder> Orders { get; set; } = new List<DeliveryOrder>();
}
