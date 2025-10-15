using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using Microsoft.Azure.Cosmos;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Repositories;

public class UserProfileRepository : CosmosRepository<UserProfile>, IUserProfileRepository
{
    public UserProfileRepository(Container container, ILogger<CosmosRepository<UserProfile>> logger) 
        : base(container, logger)
    {
    }

    public async Task<UserProfile?> GetByUserIdAsync(string userId)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId")
                .WithParameter("@userId", userId);
            
            var iterator = _container.GetItemQueryIterator<UserProfile>(query);
            var response = await iterator.ReadNextAsync();
            
            return response.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<UserProfile>> GetHighRiskUsersAsync(double riskThreshold)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.RiskScore >= @threshold ORDER BY c.RiskScore DESC")
            .WithParameter("@threshold", riskThreshold);

        var iterator = _container.GetItemQueryIterator<UserProfile>(query);
        var profiles = new List<UserProfile>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            profiles.AddRange(response);
        }

        return profiles;
    }
}
