using GlobalPaymentFraudDetection.Repositories;

namespace GlobalPaymentFraudDetection.Infrastructure;

public interface IUnitOfWork : IDisposable
{
    IUserProfileRepository UserProfiles { get; }
    ITransactionRepository Transactions { get; }
    IFraudAlertRepository FraudAlerts { get; }
    
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
