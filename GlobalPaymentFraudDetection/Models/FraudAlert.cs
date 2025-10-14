namespace GlobalPaymentFraudDetection.Models;

public class FraudAlert
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public double FraudProbability { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "MEDIUM";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "UNRESOLVED";
    public string AssignedTo { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }
    public List<string> Reasons { get; set; } = new();
}
