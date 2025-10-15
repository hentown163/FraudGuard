using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Repositories;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<List<Transaction>> GetUserTransactionsAsync(string userId, int limit = 100);
    Task<List<Transaction>> GetRecentTransactionsAsync(TimeSpan timeWindow);
    Task<List<Transaction>> GetFraudulentTransactionsAsync(DateTime? startDate = null);
    Task<decimal> GetUserTransactionVolumeAsync(string userId, TimeSpan timeWindow);
    Task<List<Transaction>> GetTransactionsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<Transaction>> SearchTransactionsAsync(string? userId, string? status, double minScore, DateTime startDate, DateTime endDate);
}
