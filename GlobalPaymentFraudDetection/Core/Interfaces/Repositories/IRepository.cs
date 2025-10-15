using System.Linq.Expressions;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(string id, string partitionKey);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity, string partitionKey);
    Task DeleteAsync(string id, string partitionKey);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
}
