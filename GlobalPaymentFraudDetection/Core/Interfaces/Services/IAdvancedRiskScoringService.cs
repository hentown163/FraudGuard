using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface IAdvancedRiskScoringService
{
    Task<double> CalculateDeviceRiskScoreAsync(Transaction transaction);
    Task<double> CalculateVelocityRiskScoreAsync(string userId, Transaction transaction);
    Task<double> CalculateGeolocationRiskScoreAsync(string ipAddress, string userId);
    Task<double> CalculateAmountRiskScoreAsync(decimal amount, string userId);
    Task<double> CalculateTimeBasedRiskScoreAsync(DateTime transactionTime, string userId);
    Task<Dictionary<string, double>> CalculateAllRiskScoresAsync(Transaction transaction, UserProfile? userProfile);
}
