namespace OrbiApp.Models.Commerce;

public class ShoppingCartItem
{
    public int ShoppingCartItemId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}