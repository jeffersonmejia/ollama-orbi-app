namespace OrbiApp.Models.Commerce;

public class PaymentTransaction
{
    public int PaymentTransactionId { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public string ClientTransactionId { get; set; } = string.Empty;
    // Datos históricos de pagos conservados para transacciones ya registradas.
    public string? PayphonePaymentUrl { get; set; }
    public string? PayphoneTransactionId { get; set; }
    public int AmountInCents { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }


    public string Provider { get; set; } = "PayPal";

    public string? PayPalOrderId { get; set; }

    public string? PayPalCaptureId { get; set; }

    public string? PayPalApprovalUrl { get; set; }

    public string? GatewayResponse { get; set; }

}
