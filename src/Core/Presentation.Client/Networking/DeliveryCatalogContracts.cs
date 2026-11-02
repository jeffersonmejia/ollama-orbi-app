using System.ComponentModel.DataAnnotations;

namespace OrbiApp.Presentation.Client.Networking;

public sealed record DeliveryCatalogResponse(
    IReadOnlyList<DeliveryCatalogProduct> Products,
    int TotalCount,
    int PageIndex,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage,
    bool IsCustomer,
    bool HasAddress,
    DeliveryCatalogLocation? Location,
    DeliveryCatalogAddress? PrimaryAddress);

public sealed record DeliveryCatalogProduct(
    int Id,
    string Name,
    string? ImageUrl,
    decimal Price,
    int Stock,
    string StoreName,
    string StoreCategory,
    string StoreAddress,
    string? CityName,
    int CartQuantity);

public sealed record DeliveryCatalogLocation(
    string? ProvinceCode,
    string? ProvinceName,
    string? CityCode,
    string? CityName);

public sealed record DeliveryCatalogAddress(
    long Id,
    string Label,
    string AddressLine1,
    string AddressLine2,
    string ProvinceCode,
    string CityCode,
    string Reference);

public sealed record DeliveryCartResponse(
    IReadOnlyList<DeliveryCartItemResponse> Items,
    decimal Total,
    bool HasAddress);

public sealed record DeliveryCartItemResponse(
    long CartItemId,
    int ProductId,
    string ProductName,
    string StoreName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public sealed record UpdateDeliveryCartQuantityRequest([property: Range(1, 999)] int Quantity);
