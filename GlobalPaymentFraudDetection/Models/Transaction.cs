namespace GlobalPaymentFraudDetection.Models;

public class Transaction
{
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string PaymentGateway { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
