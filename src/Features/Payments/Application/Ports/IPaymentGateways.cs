namespace OrbiApp.Application.Features.Payments.Ports;

public interface IPayPalGateway
{
    string PublicClientId { get; }
    Task<PayPalOrderResult> CreateOrderAsync(decimal total, string reference, string? returnUrl = null, string? cancelUrl = null);
    Task<PayPalCaptureResult> CaptureOrderAsync(string orderId);
}

public interface IPayPhoneGateway
{
    Task<string> CreatePaymentLinkAsync(decimal total, string clientTransactionId, string reference);
}

public sealed class PayPalOrderResult
{
    public string OrderId { get; set; } = string.Empty;
    public string ApprovalUrl { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}

public sealed class PayPalCaptureResult
{
    public string Status { get; set; } = string.Empty;
    public string CaptureId { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}
