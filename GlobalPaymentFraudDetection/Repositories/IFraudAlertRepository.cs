using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Repositories;

public interface IFraudAlertRepository : IRepository<FraudAlert>
{
    Task<List<FraudAlert>> GetUnresolvedAlertsAsync();
    Task<List<FraudAlert>> GetAlertsByUserIdAsync(string userId);
    Task<List<FraudAlert>> GetAlertsBySeverityAsync(string severity);
}
