namespace SakilaApp.Models.Commerce;

public class ShoppingCartItem
{
    public int ShoppingCartItemId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public int FilmStockId { get; set; }
    public FilmStock FilmStock { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}