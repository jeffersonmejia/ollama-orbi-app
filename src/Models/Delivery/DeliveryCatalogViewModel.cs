using SakilaApp.Models.Identity;

namespace SakilaApp.Models.Delivery;

public sealed class DeliveryCatalogViewModel
{
    public IReadOnlyList<DeliveryProduct> Products { get; init; } = Array.Empty<DeliveryProduct>();
    public IReadOnlyList<UserAddress> Addresses { get; init; } = Array.Empty<UserAddress>();
}
