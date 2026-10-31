namespace OrbiApp.Models.Commerce;

public class Product
{
    public int ProductId { get; set; }
    public int SourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
