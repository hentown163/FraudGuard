using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface IFraudScoringService
{
    Task<FraudScoreResponse> ScoreTransactionAsync(Transaction transaction);
}
