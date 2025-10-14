namespace GlobalPaymentFraudDetection.Models;

public class PaymentGatewayTransaction
{
    public string GatewayTransactionId { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = string.Empty;
    public string PaymentMethodId { get; set; } = string.Empty;
    public string PaymentMethodType { get; set; } = string.Empty;
    public Dictionary<string, object> RawData { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StripeWebhookEvent
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public object Data { get; set; } = new();
    public DateTime Created { get; set; }
}

public class PayPalWebhookEvent
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public object Resource { get; set; } = new();
    public DateTime CreateTime { get; set; }
}
