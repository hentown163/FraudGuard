namespace GlobalPaymentFraudDetection.Models;

public enum PaymentGatewayType
{
    Stripe,
    PayPal,
    Braintree,
    AuthorizeNet
}

public class PaymentGatewayTransaction
{
    public string GatewayTransactionId { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public PaymentGatewayType GatewayType { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = string.Empty;
    public string PaymentMethodId { get; set; } = string.Empty;
    public string PaymentMethodType { get; set; } = string.Empty;
    public Dictionary<string, object> RawData { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? FailoverReason { get; set; }
}

public class PaymentGatewayResult
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string? ErrorMessage { get; set; }
    public PaymentGatewayType GatewayUsed { get; set; }
    public PaymentGatewayTransaction? Transaction { get; set; }
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

public class BraintreeWebhookEvent
{
    public string Kind { get; set; } = string.Empty;
    public object Subject { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class AuthorizeNetWebhookEvent
{
    public string NotificationId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public object Payload { get; set; } = new();
    public DateTime EventDate { get; set; }
}
