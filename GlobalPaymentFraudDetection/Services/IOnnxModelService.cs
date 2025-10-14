using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface IOnnxModelService
{
    Task<double> PredictFraudProbabilityAsync(Transaction transaction, UserProfile? userProfile, BehavioralData behavioralData);
}
