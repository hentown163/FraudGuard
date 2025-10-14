using Microsoft.Azure.Cosmos;
using GlobalPaymentFraudDetection.Repositories;

namespace GlobalPaymentFraudDetection.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly CosmosClient _cosmosClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UnitOfWork> _logger;
    private readonly ILoggerFactory _loggerFactory;
    
    private IUserProfileRepository? _userProfiles;
    private ITransactionRepository? _transactions;
    private IFraudAlertRepository? _fraudAlerts;

    private Database? _database;
    private bool _disposed;

    public UnitOfWork(
        CosmosClient cosmosClient, 
        IConfiguration configuration,
        ILogger<UnitOfWork> logger,
        ILoggerFactory loggerFactory)
    {
        _cosmosClient = cosmosClient;
        _configuration = configuration;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    private Database Database
    {
        get
        {
            if (_database == null)
            {
                var databaseName = _configuration["Cosmos:DatabaseName"] ?? "FraudDetection";
                _database = _cosmosClient.GetDatabase(databaseName);
            }
            return _database;
        }
    }

    public IUserProfileRepository UserProfiles
    {
        get
        {
            if (_userProfiles == null)
            {
                var container = Database.GetContainer("UserProfiles");
                var logger = _loggerFactory.CreateLogger<CosmosRepository<Models.UserProfile>>();
                _userProfiles = new UserProfileRepository(container, logger);
            }
            return _userProfiles;
        }
    }

    public ITransactionRepository Transactions
    {
        get
        {
            if (_transactions == null)
            {
                var container = Database.GetContainer("Transactions");
                var logger = _loggerFactory.CreateLogger<CosmosRepository<Models.Transaction>>();
                _transactions = new TransactionRepository(container, logger);
            }
            return _transactions;
        }
    }

    public IFraudAlertRepository FraudAlerts
    {
        get
        {
            if (_fraudAlerts == null)
            {
                var container = Database.GetContainer("FraudAlerts");
                var logger = _loggerFactory.CreateLogger<CosmosRepository<Models.FraudAlert>>();
                _fraudAlerts = new FraudAlertRepository(container, logger);
            }
            return _fraudAlerts;
        }
    }

    public async Task<int> SaveChangesAsync()
    {
        return await Task.FromResult(0);
    }

    public async Task BeginTransactionAsync()
    {
        _logger.LogInformation("Beginning transaction (Note: Cosmos DB uses optimistic concurrency)");
        await Task.CompletedTask;
    }

    public async Task CommitTransactionAsync()
    {
        _logger.LogInformation("Committing transaction");
        await Task.CompletedTask;
    }

    public async Task RollbackTransactionAsync()
    {
        _logger.LogWarning("Rolling back transaction");
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
        }
    }
}
