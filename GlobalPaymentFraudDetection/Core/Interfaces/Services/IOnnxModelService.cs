using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface IOnnxModelService
{
    Task<double> PredictFraudProbabilityAsync(Transaction transaction, UserProfile? userProfile, BehavioralData behavioralData);
}
