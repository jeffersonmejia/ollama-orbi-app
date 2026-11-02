using OrbiApp.Models.Identity;
using OrbiApp.Models;

namespace OrbiApp.Models.Delivery;

public sealed class DeliveryCatalogViewModel
{
    public PaginatedList<DeliveryProduct> Products { get; init; } = new(new(), 0, 1, 12);
    public IReadOnlyList<UserAddress> Addresses { get; init; } = Array.Empty<UserAddress>();
    public IReadOnlyList<EcuadorProvince> Provinces { get; init; } = Array.Empty<EcuadorProvince>();
    public IReadOnlyList<EcuadorCity> Cities { get; init; } = Array.Empty<EcuadorCity>();
    public IReadOnlyDictionary<int, int> CartQuantities { get; init; } = new Dictionary<int, int>();
}
