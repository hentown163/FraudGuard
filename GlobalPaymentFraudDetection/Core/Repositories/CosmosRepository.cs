using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using System.Linq.Expressions;

namespace GlobalPaymentFraudDetection.Core.Repositories;

public class CosmosRepository<T> : IRepository<T> where T : class
{
    protected readonly Container _container;
    protected readonly ILogger<CosmosRepository<T>> _logger;

    public CosmosRepository(Container container, ILogger<CosmosRepository<T>> logger)
    {
        _container = container;
        _logger = logger;
    }

    public virtual async Task<T?> GetByIdAsync(string id, string partitionKey)
    {
        try
        {
            var response = await _container.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        var query = _container.GetItemQueryIterator<T>(new QueryDefinition("SELECT * FROM c"));
        var results = new List<T>();

        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        var queryable = _container.GetItemLinqQueryable<T>().Where(predicate);
        
        using var iterator = queryable.ToFeedIterator();
        var results = new List<T>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        var response = await _container.CreateItemAsync(entity);
        return response.Resource;
    }

    public virtual async Task UpdateAsync(T entity, string partitionKey)
    {
        await _container.UpsertItemAsync(entity, new PartitionKey(partitionKey));
    }

    public virtual async Task DeleteAsync(string id, string partitionKey)
    {
        await _container.DeleteItemAsync<T>(id, new PartitionKey(partitionKey));
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        IQueryable<T> queryable = _container.GetItemLinqQueryable<T>();
        
        if (predicate != null)
        {
            queryable = queryable.Where(predicate);
        }

        using var iterator = queryable.ToFeedIterator();
        var count = 0;

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            count += response.Count();
        }

        return count;
    }
}
