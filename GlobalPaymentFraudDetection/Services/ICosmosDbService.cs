using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface ICosmosDbService
{
    Task<UserProfile?> GetUserProfileAsync(string userId);
    Task UpsertUserProfileAsync(UserProfile profile);
    Task<List<Transaction>> GetUserTransactionsAsync(string userId, int limit = 100);
    Task StoreTransactionAsync(Transaction transaction);
}
