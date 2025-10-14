using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface IFraudRulesEngine
{
    Task<List<string>> EvaluateRulesAsync(Transaction transaction, UserProfile? userProfile);
    Task<bool> ShouldBlockTransactionAsync(Transaction transaction);
    Task<bool> RequiresManualReviewAsync(Transaction transaction, double fraudScore);
}
