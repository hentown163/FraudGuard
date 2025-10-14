using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface IBehavioralAnalysisService
{
    Task<BehavioralData> AnalyzeTransactionBehaviorAsync(Transaction transaction, UserProfile? userProfile);
    Task<GeoLocationData?> GetGeoLocationAsync(string ipAddress);
    Task<TransactionVelocity> CalculateVelocityAsync(string userId);
}
