using System.ComponentModel.DataAnnotations;

namespace OrbiApp.Presentation.Client.Networking;

public sealed record DeliveryOrdersResponse(
    IReadOnlyList<DeliveryOrderSummary> Orders,
    int TotalCount,
    int PageIndex,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

public sealed record DeliveryOrderSummary(
    int DeliveryOrderId,
    DateTime CreatedAt,
    string StoreName,
    IReadOnlyList<DeliveryOrderItemSummary> Items,
    string DeliveryAddress,
    decimal Total,
    string Status);

public sealed record DeliveryOrderItemSummary(int Quantity, string ProductName);

public sealed record DeliveryPaymentsResponse(
    IReadOnlyList<DeliveryPaymentSummary> Payments,
    int TotalCount,
    int PageIndex,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

public sealed record DeliveryPaymentSummary(
    long PaymentId,
    int DeliveryOrderId,
    string StoreName,
    string Provider,
    decimal Amount,
    string Status,
    DateTimeOffset CreatedAt);

public sealed class UpdateDeliveryOrderStatusRequest
{
    [Required, StringLength(30)]
    public string Status { get; init; } = string.Empty;
}
