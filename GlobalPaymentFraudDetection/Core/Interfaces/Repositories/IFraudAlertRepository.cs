using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Repositories;

public interface IFraudAlertRepository : IRepository<FraudAlert>
{
    Task<List<FraudAlert>> GetUnresolvedAlertsAsync();
    Task<List<FraudAlert>> GetAlertsByUserIdAsync(string userId);
    Task<List<FraudAlert>> GetAlertsBySeverityAsync(string severity);
    Task<List<FraudAlert>> GetAlertsByDateRangeAsync(DateTime startDate, DateTime endDate);
}
