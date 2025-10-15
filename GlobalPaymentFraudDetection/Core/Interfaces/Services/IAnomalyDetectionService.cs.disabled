using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface IAnomalyDetectionService
{
    Task<AnomalyDetectionResult> DetectAnomaliesAsync(List<TransactionDataPoint> dataPoints);
    Task<bool> IsTransactionAnomalousAsync(Transaction transaction, List<Transaction> historicalTransactions);
}

public class AnomalyDetectionResult
{
    public bool IsAnomaly { get; set; }
    public double AnomalyScore { get; set; }
    public string? Severity { get; set; }
    public List<string> DetectedPatterns { get; set; } = new();
}

public class TransactionDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}
