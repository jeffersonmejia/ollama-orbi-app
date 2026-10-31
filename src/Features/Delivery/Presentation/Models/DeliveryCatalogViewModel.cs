using OrbiApp.Models.Identity;
using OrbiApp.Models;

namespace OrbiApp.Models.Delivery;

public sealed class DeliveryCatalogViewModel
{
    public PaginatedList<DeliveryProduct> Products { get; init; } = new(new(), 0, 1, 12);
    public IReadOnlyList<UserAddress> Addresses { get; init; } = Array.Empty<UserAddress>();
}
