namespace SakilaApp.Models.Delivery;

public class DeliveryPayment
{
    public long PaymentId { get; set; }
    public int DeliveryOrderId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedAt { get; set; }

    public DeliveryOrder Order { get; set; } = null!;
}
