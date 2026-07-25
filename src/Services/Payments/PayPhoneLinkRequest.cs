namespace SakilaApp.Services.Payments;

public class PayPhoneLinkRequest
{
    public int Amount { get; set; }
    public int AmountWithoutTax { get; set; }
    public int AmountWithTax { get; set; }
    public int Tax { get; set; }
    public int Service { get; set; }
    public int Tip { get; set; }
    public string Currency { get; set; } = "USD";
    public string Reference { get; set; } = string.Empty;
    public string ClientTransactionId { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string AdditionalData { get; set; } = string.Empty;
    public bool OneTime { get; set; } = true;
    public int ExpireIn { get; set; } = 0;
    public bool IsAmountEditable { get; set; } = false;
}