using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface IAzureOpenAIService
{
    Task<string> AnalyzeFraudPatternAsync(Transaction transaction, FraudScoreResponse fraudScore);
    Task<string> GenerateFraudSummaryAsync(List<Transaction> transactions);
    Task<string> GetFraudInsightsAsync(string query);
    Task<List<string>> DetectAnomaliesWithAIAsync(Transaction transaction, List<Transaction> historicalTransactions);
    Task<string> ChatWithFraudAssistantAsync(string userMessage, List<Transaction> context);
}
