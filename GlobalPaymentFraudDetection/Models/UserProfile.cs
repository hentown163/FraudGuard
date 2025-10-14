namespace GlobalPaymentFraudDetection.Models;

public class UserProfile
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public decimal AvgAmount { get; set; }
    public decimal TotalSpent { get; set; }
    public string LastLocation { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public int SuspiciousFlags { get; set; }
    public List<string> UsedIpAddresses { get; set; } = new();
    public List<string> UsedDevices { get; set; } = new();
    public DateTime FirstTransactionDate { get; set; }
    public DateTime LastTransactionDate { get; set; }
    public int DeclinedTransactions { get; set; }
    public int ChargebackCount { get; set; }
    public double VelocityScore { get; set; }
}
