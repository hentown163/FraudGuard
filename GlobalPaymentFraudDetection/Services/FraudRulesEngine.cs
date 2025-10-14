using GlobalPaymentFraudDetection.Models;
using GlobalPaymentFraudDetection.Infrastructure;

namespace GlobalPaymentFraudDetection.Services;

public class FraudRulesEngine : IFraudRulesEngine
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FraudRulesEngine> _logger;
    private readonly IConfiguration _configuration;

    private readonly HashSet<string> _blockedCountries = new() { "KP", "IR", "SY" };
    private readonly decimal _highAmountThreshold = 10000;
    private readonly int _maxTransactionsPerHour = 15;

    public FraudRulesEngine(
        IUnitOfWork unitOfWork,
        ILogger<FraudRulesEngine> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<List<string>> EvaluateRulesAsync(Transaction transaction, UserProfile? userProfile)
    {
        using var activity = DistributedTracing.ActivitySource.StartFraudDetectionActivity(
            "EvaluateRules", transaction.TransactionId);

        var violations = new List<string>();

        if (!string.IsNullOrEmpty(transaction.Country) && _blockedCountries.Contains(transaction.Country))
        {
            violations.Add("BLOCKED_COUNTRY");
        }

        if (transaction.Amount > _highAmountThreshold)
        {
            violations.Add("HIGH_AMOUNT");
        }

        var recentTransactions = await _unitOfWork.Transactions
            .GetUserTransactionsAsync(transaction.UserId, 100);

        var transactionsInLastHour = recentTransactions
            .Count(t => t.Timestamp > DateTime.UtcNow.AddHours(-1));

        if (transactionsInLastHour > _maxTransactionsPerHour)
        {
            violations.Add("VELOCITY_EXCEEDED");
        }

        var duplicateTransactions = recentTransactions
            .Where(t => t.Amount == transaction.Amount && 
                       t.Timestamp > DateTime.UtcNow.AddMinutes(-5) &&
                       t.TransactionId != transaction.TransactionId)
            .ToList();

        if (duplicateTransactions.Any())
        {
            violations.Add("DUPLICATE_TRANSACTION");
        }

        if (userProfile != null && userProfile.IsBlacklisted)
        {
            violations.Add("BLACKLISTED_USER");
        }

        var hour = transaction.Timestamp.Hour;
        if (hour >= 2 && hour <= 4 && transaction.Amount > 5000)
        {
            violations.Add("SUSPICIOUS_TIME_AMOUNT");
        }

        var uniqueCountriesIn24h = recentTransactions
            .Where(t => t.Timestamp > DateTime.UtcNow.AddHours(-24))
            .Select(t => t.Country)
            .Distinct()
            .Count();

        if (uniqueCountriesIn24h > 5)
        {
            violations.Add("MULTIPLE_COUNTRIES");
        }

        activity?.SetTag("rules.violations_count", violations.Count);
        activity?.RecordFraudEvent("RulesEvaluated", new Dictionary<string, object>
        {
            { "violations", string.Join(", ", violations) },
            { "transaction_id", transaction.TransactionId }
        });

        _logger.LogInformation(
            "Rules evaluation for transaction {TransactionId}: {ViolationCount} violations - {Violations}",
            transaction.TransactionId, violations.Count, string.Join(", ", violations));

        return violations;
    }

    public async Task<bool> ShouldBlockTransactionAsync(Transaction transaction)
    {
        var violations = await EvaluateRulesAsync(transaction, null);

        var blockingRules = new HashSet<string> 
        { 
            "BLOCKED_COUNTRY", 
            "BLACKLISTED_USER", 
            "DUPLICATE_TRANSACTION" 
        };

        return violations.Any(v => blockingRules.Contains(v));
    }

    public async Task<bool> RequiresManualReviewAsync(Transaction transaction, double fraudScore)
    {
        var manualReviewThreshold = _configuration.GetValue<double>("FraudDetection:ManualReviewThreshold", 0.5);
        var autoDeclineThreshold = _configuration.GetValue<double>("FraudDetection:Threshold", 0.7);

        if (fraudScore >= manualReviewThreshold && fraudScore < autoDeclineThreshold)
        {
            return true;
        }

        var violations = await EvaluateRulesAsync(transaction, null);
        var reviewRules = new HashSet<string> 
        { 
            "HIGH_AMOUNT", 
            "VELOCITY_EXCEEDED", 
            "SUSPICIOUS_TIME_AMOUNT",
            "MULTIPLE_COUNTRIES"
        };

        return violations.Any(v => reviewRules.Contains(v));
    }
}
