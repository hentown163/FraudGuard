using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Repositories;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<List<Transaction>> GetUserTransactionsAsync(string userId, int limit = 100);
    Task<List<Transaction>> GetRecentTransactionsAsync(TimeSpan timeWindow);
    Task<List<Transaction>> GetFraudulentTransactionsAsync(DateTime? startDate = null);
    Task<decimal> GetUserTransactionVolumeAsync(string userId, TimeSpan timeWindow);
}
