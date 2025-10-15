using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface ISiftScienceService
{
    Task<SiftScienceResponse> ScoreTransactionAsync(Transaction transaction);
    Task<bool> ReportChargebackAsync(string transactionId, decimal amount);
    Task<bool> ReportFraudAsync(string userId, string transactionId);
}

public class SiftScienceResponse
{
    public double Score { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
    public Dictionary<string, object> RawResponse { get; set; } = new();
}
