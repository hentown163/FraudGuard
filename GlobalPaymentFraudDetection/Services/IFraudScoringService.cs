using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface IFraudScoringService
{
    Task<FraudScoreResponse> ScoreTransactionAsync(Transaction transaction);
}
