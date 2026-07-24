namespace SakilaApp.Models.Commerce;

public class PurchaseOrderDetail
{
    public int PurchaseOrderDetailId { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int FilmStockId { get; set; }
    public string FilmTitle { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}