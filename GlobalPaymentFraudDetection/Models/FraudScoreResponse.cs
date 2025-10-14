namespace GlobalPaymentFraudDetection.Models;

public class FraudScoreResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public double FraudProbability { get; set; }
    public bool IsFraudulent { get; set; }
    public string Decision { get; set; } = "PENDING";
    public string Reason { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, double> RiskFactors { get; set; } = new();
    public string ReviewStatus { get; set; } = "AUTO";
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
}
