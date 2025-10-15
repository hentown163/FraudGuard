namespace GlobalPaymentFraudDetection.Functions.Models;

public class FraudAnalysisRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string UserEmail { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string PaymentGateway { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public Dictionary<string, string>? Metadata { get; set; }
}

public class FraudAnalysisResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public double FraudScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public List<string> RiskFactors { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class DailyReportSummary
{
    public DateTime ReportDate { get; set; }
    public int TotalTransactions { get; set; }
    public int FraudulentTransactions { get; set; }
    public int SuspiciousTransactions { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal FraudAmount { get; set; }
    public double FraudRate { get; set; }
    public List<string> TopRiskFactors { get; set; } = new();
    public Dictionary<string, int> GatewayDistribution { get; set; } = new();
}

public class AlertMessage
{
    public string AlertId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
