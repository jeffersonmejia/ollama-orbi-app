using OrbiApp.Models.Identity;

namespace OrbiApp.Models.Delivery;

public class DeliveryStore
{
    public int DeliveryStoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ProvinceCode { get; set; }
    public string? CityCode { get; set; }
    public string? OwnerUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<DeliveryProduct> Products { get; set; } = new List<DeliveryProduct>();
    public ICollection<DeliveryOrder> Orders { get; set; } = new List<DeliveryOrder>();
    public EcuadorProvince? Province { get; set; }
    public EcuadorCity? City { get; set; }
}

public static class DeliveryStoreCategories
{
    public const string Restaurants = "Restaurantes";
    public const string Pharmacy = "Farmacia";
    public const string Supermarket = "Supermercado";

    public static readonly IReadOnlyList<string> All = [Restaurants, Pharmacy, Supermarket];

    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "restaurante" or "restaurantes" => Restaurants,
        "farmacia" or "farmacias" => Pharmacy,
        "supermercado" or "supermercados" => Supermarket,
        _ => null
    };
}
