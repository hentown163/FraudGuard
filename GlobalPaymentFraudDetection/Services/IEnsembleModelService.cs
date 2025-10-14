using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface IEnsembleModelService
{
    Task<double> PredictWithEnsembleAsync(Transaction transaction, UserProfile? userProfile, BehavioralData behavioralData);
    Task<Dictionary<string, double>> GetModelPredictionsAsync(Transaction transaction, UserProfile? userProfile, BehavioralData behavioralData);
}
