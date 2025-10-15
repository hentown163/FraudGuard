using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using Microsoft.Azure.Cosmos;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Repositories;

public class TransactionRepository : CosmosRepository<Transaction>, ITransactionRepository
{
    public TransactionRepository(Container container, ILogger<CosmosRepository<Transaction>> logger) 
        : base(container, logger)
    {
    }

    public async Task<List<Transaction>> GetUserTransactionsAsync(string userId, int limit = 100)
    {
        var query = new QueryDefinition(
            "SELECT TOP @limit * FROM c WHERE c.UserId = @userId ORDER BY c.Timestamp DESC")
            .WithParameter("@userId", userId)
            .WithParameter("@limit", limit);

        var iterator = _container.GetItemQueryIterator<Transaction>(query);
        var transactions = new List<Transaction>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            transactions.AddRange(response);
        }

        return transactions;
    }

    public async Task<List<Transaction>> GetRecentTransactionsAsync(TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
        
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.Timestamp >= @cutoffTime ORDER BY c.Timestamp DESC")
            .WithParameter("@cutoffTime", cutoffTime);

        var iterator = _container.GetItemQueryIterator<Transaction>(query);
        var transactions = new List<Transaction>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            transactions.AddRange(response);
        }

        return transactions;
    }

    public async Task<List<Transaction>> GetFraudulentTransactionsAsync(DateTime? startDate = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.IsFraudulent = true AND c.Timestamp >= @startDate ORDER BY c.Timestamp DESC")
            .WithParameter("@startDate", start);

        var iterator = _container.GetItemQueryIterator<Transaction>(query);
        var transactions = new List<Transaction>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            transactions.AddRange(response);
        }

        return transactions;
    }

    public async Task<decimal> GetUserTransactionVolumeAsync(string userId, TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
        
        var query = new QueryDefinition(
            "SELECT VALUE SUM(c.Amount) FROM c WHERE c.UserId = @userId AND c.Timestamp >= @cutoffTime")
            .WithParameter("@userId", userId)
            .WithParameter("@cutoffTime", cutoffTime);

        var iterator = _container.GetItemQueryIterator<decimal>(query);
        
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }

        return 0;
    }

    public async Task<List<Transaction>> GetTransactionsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.Timestamp >= @startDate AND c.Timestamp <= @endDate ORDER BY c.Timestamp DESC")
            .WithParameter("@startDate", startDate)
            .WithParameter("@endDate", endDate);

        var iterator = _container.GetItemQueryIterator<Transaction>(query);
        var transactions = new List<Transaction>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            transactions.AddRange(response);
        }

        return transactions;
    }

    public async Task<List<Transaction>> SearchTransactionsAsync(string? userId, string? status, double minScore, DateTime startDate, DateTime endDate)
    {
        var conditions = new List<string> { "c.Timestamp >= @startDate", "c.Timestamp <= @endDate" };
        var queryDef = new QueryDefinition("SELECT * FROM c WHERE ");

        if (!string.IsNullOrEmpty(userId))
        {
            conditions.Add("c.UserId = @userId");
            queryDef = queryDef.WithParameter("@userId", userId);
        }

        if (!string.IsNullOrEmpty(status))
        {
            conditions.Add("c.Status = @status");
            queryDef = queryDef.WithParameter("@status", status);
        }

        if (minScore > 0)
        {
            conditions.Add("c.FraudScore >= @minScore");
            queryDef = queryDef.WithParameter("@minScore", minScore);
        }

        queryDef = queryDef.WithParameter("@startDate", startDate);
        queryDef = queryDef.WithParameter("@endDate", endDate);

        var fullQuery = $"SELECT * FROM c WHERE {string.Join(" AND ", conditions)} ORDER BY c.Timestamp DESC";
        queryDef = new QueryDefinition(fullQuery);

        if (!string.IsNullOrEmpty(userId))
            queryDef = queryDef.WithParameter("@userId", userId);
        if (!string.IsNullOrEmpty(status))
            queryDef = queryDef.WithParameter("@status", status);
        if (minScore > 0)
            queryDef = queryDef.WithParameter("@minScore", minScore);

        queryDef = queryDef.WithParameter("@startDate", startDate);
        queryDef = queryDef.WithParameter("@endDate", endDate);

        var iterator = _container.GetItemQueryIterator<Transaction>(queryDef);
        var transactions = new List<Transaction>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            transactions.AddRange(response);
        }

        return transactions;
    }
}
