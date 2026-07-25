namespace SakilaApp.Models.Delivery;

public class AdminDashboardViewModel
{
    public PaginatedList<DeliveryStore> Stores { get; set; } = new();
    public PaginatedList<DeliveryOrder> Orders { get; set; } = new();
}