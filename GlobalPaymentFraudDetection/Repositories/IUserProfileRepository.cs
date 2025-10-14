using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Repositories;

public interface IUserProfileRepository : IRepository<UserProfile>
{
    Task<UserProfile?> GetByUserIdAsync(string userId);
    Task<List<UserProfile>> GetHighRiskUsersAsync(double riskThreshold);
}
