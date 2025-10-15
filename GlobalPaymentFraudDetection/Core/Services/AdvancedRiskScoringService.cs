using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using GlobalPaymentFraudDetection.Infrastructure;
using System.Diagnostics;

namespace GlobalPaymentFraudDetection.Core.Services;

public class AdvancedRiskScoringService : IAdvancedRiskScoringService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdvancedRiskScoringService> _logger;

    public AdvancedRiskScoringService(
        IUnitOfWork unitOfWork,
        ILogger<AdvancedRiskScoringService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<double> CalculateDeviceRiskScoreAsync(Transaction transaction)
    {
        using var activity = DistributedTracing.ActivitySource.StartFraudDetectionActivity(
            "CalculateDeviceRisk", transaction.TransactionId);

        try
        {
            var deviceId = transaction.DeviceId;
            if (string.IsNullOrEmpty(deviceId))
            {
                return 0.5;
            }

            var recentTransactions = await _unitOfWork.Transactions
                .GetRecentTransactionsAsync(TimeSpan.FromHours(24));

            var deviceTransactions = recentTransactions
                .Where(t => t.DeviceId == deviceId)
                .ToList();

            var fraudulentCount = deviceTransactions.Count(t => t.IsFraudulent);
            var totalCount = deviceTransactions.Count;

            if (totalCount == 0) return 0.3;

            var fraudRate = (double)fraudulentCount / totalCount;
            
            var uniqueUserCount = deviceTransactions.Select(t => t.UserId).Distinct().Count();
            var multiUserPenalty = uniqueUserCount > 5 ? 0.3 : 0;

            var riskScore = Math.Min(fraudRate + multiUserPenalty, 1.0);

            activity?.SetTag("device.fraud_rate", fraudRate);
            activity?.SetTag("device.unique_users", uniqueUserCount);

            return riskScore;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating device risk score");
            activity?.RecordException(ex);
            return 0.5;
        }
    }

    public async Task<double> CalculateVelocityRiskScoreAsync(string userId, Transaction transaction)
    {
        using var activity = DistributedTracing.ActivitySource.StartFraudDetectionActivity(
            "CalculateVelocityRisk", transaction.TransactionId);

        try
        {
            var last1Hour = await _unitOfWork.Transactions
                .GetUserTransactionVolumeAsync(userId, TimeSpan.FromHours(1));
            
            var last24Hours = await _unitOfWork.Transactions
                .GetUserTransactionVolumeAsync(userId, TimeSpan.FromHours(24));

            var hourlyTransactions = await _unitOfWork.Transactions.GetUserTransactionsAsync(userId, 100);
            var transactionsInLastHour = hourlyTransactions
                .Count(t => t.Timestamp > DateTime.UtcNow.AddHours(-1));

            var volumeRisk = 0.0;
            if (last1Hour > 10000) volumeRisk += 0.3;
            if (last24Hours > 50000) volumeRisk += 0.2;

            var frequencyRisk = 0.0;
            if (transactionsInLastHour > 10) frequencyRisk += 0.3;
            if (transactionsInLastHour > 20) frequencyRisk += 0.2;

            var totalRisk = Math.Min(volumeRisk + frequencyRisk, 1.0);

            activity?.SetTag("velocity.1h_volume", last1Hour);
            activity?.SetTag("velocity.24h_volume", last24Hours);
            activity?.SetTag("velocity.1h_count", transactionsInLastHour);

            return totalRisk;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating velocity risk score");
            activity?.RecordException(ex);
            return 0.5;
        }
    }

    public async Task<double> CalculateGeolocationRiskScoreAsync(string ipAddress, string userId)
    {
        using var activity = DistributedTracing.ActivitySource.StartFraudDetectionActivity(
            "CalculateGeolocationRisk");

        try
        {
            var userTransactions = await _unitOfWork.Transactions.GetUserTransactionsAsync(userId, 20);
            
            var recentLocations = userTransactions
                .Where(t => !string.IsNullOrEmpty(t.Country))
                .Select(t => t.Country)
                .Distinct()
                .ToList();

            var highRiskCountries = new HashSet<string> { "NG", "PK", "VN", "ID", "RO" };
            
            var locationRisk = 0.0;

            if (recentLocations.Count > 5)
            {
                locationRisk += 0.4;
            }

            if (recentLocations.Any(c => highRiskCountries.Contains(c ?? "")))
            {
                locationRisk += 0.3;
            }

            var lastTransaction = userTransactions.FirstOrDefault();
            if (lastTransaction != null && !string.IsNullOrEmpty(lastTransaction.Country))
            {
                var timeSinceLastTransaction = DateTime.UtcNow - lastTransaction.Timestamp;
                
                if (timeSinceLastTransaction < TimeSpan.FromHours(1) && 
                    lastTransaction.Country != userTransactions.Skip(1).FirstOrDefault()?.Country)
                {
                    locationRisk += 0.3;
                }
            }

            activity?.SetTag("geo.location_count", recentLocations.Count);
            activity?.SetTag("geo.high_risk_country", recentLocations.Any(c => highRiskCountries.Contains(c ?? "")));

            return Math.Min(locationRisk, 1.0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating geolocation risk score");
            activity?.RecordException(ex);
            return 0.5;
        }
    }

    public async Task<double> CalculateAmountRiskScoreAsync(decimal amount, string userId)
    {
        using var activity = DistributedTracing.ActivitySource.StartFraudDetectionActivity(
            "CalculateAmountRisk");

        try
        {
            var userTransactions = await _unitOfWork.Transactions.GetUserTransactionsAsync(userId, 50);

            if (!userTransactions.Any())
            {
                return amount > 1000 ? 0.7 : 0.3;
            }

            var avgAmount = userTransactions.Average(t => t.Amount);
            var stdDev = CalculateStandardDeviation(userTransactions.Select(t => (double)t.Amount));

            var zScore = Math.Abs((double)(amount - avgAmount) / (stdDev > 0 ? stdDev : 1));

            var amountRisk = 0.0;
            if (zScore > 3) amountRisk = 0.8;
            else if (zScore > 2) amountRisk = 0.5;
            else if (zScore > 1.5) amountRisk = 0.3;

            if (amount > 5000) amountRisk += 0.2;

            activity?.SetTag("amount.z_score", zScore);
            activity?.SetTag("amount.avg", avgAmount);

            return Math.Min(amountRisk, 1.0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating amount risk score");
            activity?.RecordException(ex);
            return 0.5;
        }
    }

    public async Task<double> CalculateTimeBasedRiskScoreAsync(DateTime transactionTime, string userId)
    {
        using var activity = DistributedTracing.ActivitySource.StartFraudDetectionActivity(
            "CalculateTimeBasedRisk");

        try
        {
            var userTransactions = await _unitOfWork.Transactions.GetUserTransactionsAsync(userId, 100);

            var hour = transactionTime.Hour;
            var timeRisk = 0.0;

            if (hour >= 1 && hour <= 5)
            {
                timeRisk += 0.3;
            }

            var userHourlyPattern = userTransactions
                .GroupBy(t => t.Timestamp.Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            var avgHourlyTransactions = userHourlyPattern.Any() 
                ? userHourlyPattern.Values.Average() 
                : 0;

            if (userHourlyPattern.TryGetValue(hour, out var currentHourCount))
            {
                if (currentHourCount == 0 && avgHourlyTransactions > 0)
                {
                    timeRisk += 0.2;
                }
            }
            else if (avgHourlyTransactions > 0)
            {
                timeRisk += 0.3;
            }

            activity?.SetTag("time.hour", hour);
            activity?.SetTag("time.unusual_pattern", timeRisk > 0.3);

            return Math.Min(timeRisk, 1.0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating time-based risk score");
            activity?.RecordException(ex);
            return 0.5;
        }
    }

    public async Task<Dictionary<string, double>> CalculateAllRiskScoresAsync(Transaction transaction, UserProfile? userProfile)
    {
        var tasks = new[]
        {
            Task.Run(async () => ("DeviceRisk", await CalculateDeviceRiskScoreAsync(transaction))),
            Task.Run(async () => ("VelocityRisk", await CalculateVelocityRiskScoreAsync(transaction.UserId, transaction))),
            Task.Run(async () => ("GeolocationRisk", await CalculateGeolocationRiskScoreAsync(transaction.IPAddress ?? "", transaction.UserId))),
            Task.Run(async () => ("AmountRisk", await CalculateAmountRiskScoreAsync(transaction.Amount, transaction.UserId))),
            Task.Run(async () => ("TimeRisk", await CalculateTimeBasedRiskScoreAsync(transaction.Timestamp, transaction.UserId)))
        };

        var results = await Task.WhenAll(tasks);
        return results.ToDictionary(r => r.Item1, r => r.Item2);
    }

    private double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        if (!valueList.Any()) return 0;

        var avg = valueList.Average();
        var sumOfSquares = valueList.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / valueList.Count);
    }
}
