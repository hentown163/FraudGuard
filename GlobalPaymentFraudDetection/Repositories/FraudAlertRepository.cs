using Microsoft.Azure.Cosmos;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Repositories;

public class FraudAlertRepository : CosmosRepository<FraudAlert>, IFraudAlertRepository
{
    public FraudAlertRepository(Container container, ILogger<CosmosRepository<FraudAlert>> logger) 
        : base(container, logger)
    {
    }

    public async Task<List<FraudAlert>> GetUnresolvedAlertsAsync()
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.Status = 'UNRESOLVED' ORDER BY c.CreatedAt DESC");

        var iterator = _container.GetItemQueryIterator<FraudAlert>(query);
        var alerts = new List<FraudAlert>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            alerts.AddRange(response);
        }

        return alerts;
    }

    public async Task<List<FraudAlert>> GetAlertsByUserIdAsync(string userId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.UserId = @userId ORDER BY c.CreatedAt DESC")
            .WithParameter("@userId", userId);

        var iterator = _container.GetItemQueryIterator<FraudAlert>(query);
        var alerts = new List<FraudAlert>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            alerts.AddRange(response);
        }

        return alerts;
    }

    public async Task<List<FraudAlert>> GetAlertsBySeverityAsync(string severity)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.Severity = @severity ORDER BY c.CreatedAt DESC")
            .WithParameter("@severity", severity);

        var iterator = _container.GetItemQueryIterator<FraudAlert>(query);
        var alerts = new List<FraudAlert>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            alerts.AddRange(response);
        }

        return alerts;
    }
}
