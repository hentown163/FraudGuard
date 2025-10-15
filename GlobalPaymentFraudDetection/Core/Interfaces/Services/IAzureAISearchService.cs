using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface IAzureAISearchService
{
    Task<bool> IndexTransactionAsync(Transaction transaction);
    Task<List<Transaction>> SearchTransactionsAsync(string query, int maxResults = 10);
    Task<List<Transaction>> SemanticSearchAsync(string naturalLanguageQuery, int maxResults = 10);
    Task<bool> DeleteTransactionIndexAsync(string transactionId);
    Task<bool> CreateOrUpdateIndexAsync();
}
