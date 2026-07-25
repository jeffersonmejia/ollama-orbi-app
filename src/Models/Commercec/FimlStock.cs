namespace SakilaApp.Models.Commerce;

public class FilmStock
{
    public int FilmStockId { get; set; }
    public int FilmId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}