using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Functions.TimerTriggers;

public class HourlyAnomalyDetectionFunction
{
    private readonly ILogger<HourlyAnomalyDetectionFunction> _logger;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IServiceBusService _serviceBusService;
    private readonly INotificationService _notificationService;

    public HourlyAnomalyDetectionFunction(
        ILogger<HourlyAnomalyDetectionFunction> logger,
        ITransactionRepository transactionRepository,
        IServiceBusService serviceBusService,
        INotificationService notificationService)
    {
        _logger = logger;
        _transactionRepository = transactionRepository;
        _serviceBusService = serviceBusService;
        _notificationService = notificationService;
    }

    [Function("HourlyAnomalyDetection")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation($"Hourly anomaly detection triggered at: {DateTime.UtcNow}");

        try
        {
            var lastHour = DateTime.UtcNow.AddHours(-1);
            var now = DateTime.UtcNow;

            var recentTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(lastHour, now);
            var historicalTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(
                DateTime.UtcNow.AddDays(-30), lastHour);

            if (!recentTransactions.Any())
            {
                _logger.LogInformation("No transactions in the last hour");
                return;
            }

            var recentCount = recentTransactions.Count();
            var avgHistoricalHourlyCount = historicalTransactions.Count() / (30.0 * 24.0);
            var recentAvgAmount = recentTransactions.Average(t => t.Amount);
            var historicalAvgAmount = historicalTransactions.Any() ? historicalTransactions.Average(t => t.Amount) : 0;
            var recentFraudRate = recentTransactions.Count(t => t.Status == "Declined") * 100.0 / recentCount;
            var historicalFraudRate = historicalTransactions.Any() 
                ? historicalTransactions.Count(t => t.Status == "Declined") * 100.0 / historicalTransactions.Count() 
                : 0;

            var anomaliesDetected = new List<string>();

            if (recentCount > avgHistoricalHourlyCount * 3)
            {
                anomaliesDetected.Add($"Transaction volume spike: {recentCount} vs avg {avgHistoricalHourlyCount:F0}");
            }

            if (recentAvgAmount > historicalAvgAmount * 2 && historicalAvgAmount > 0)
            {
                anomaliesDetected.Add($"Average transaction amount spike: ${recentAvgAmount:F2} vs ${historicalAvgAmount:F2}");
            }

            if (recentFraudRate > historicalFraudRate * 2 && recentFraudRate > 5)
            {
                anomaliesDetected.Add($"Fraud rate spike: {recentFraudRate:F2}% vs {historicalFraudRate:F2}%");
            }

            var gatewayAnomalies = recentTransactions
                .GroupBy(t => t.Gateway)
                .Where(g => g.Count(t => t.Status == "Declined") * 100.0 / g.Count() > 30)
                .Select(g => $"High fraud rate on {g.Key}: {g.Count(t => t.Status == "Declined") * 100.0 / g.Count():F2}%");

            anomaliesDetected.AddRange(gatewayAnomalies);

            if (anomaliesDetected.Any())
            {
                _logger.LogWarning($"Anomalies detected: {string.Join(", ", anomaliesDetected)}");

                var alert = new FraudAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    TransactionId = "HOURLY_ANOMALY_" + DateTime.UtcNow.ToString("yyyyMMddHH"),
                    UserId = "SYSTEM",
                    Severity = anomaliesDetected.Count >= 3 ? "Critical" : "High",
                    Type = "Anomaly Detection",
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow,
                    RiskFactors = anomaliesDetected
                };

                await _serviceBusService.SendFraudAlertAsync(alert);

                await _notificationService.SendSmsAlertAsync(
                    "FRAUD_TEAM",
                    $"Anomalies detected in last hour: {string.Join(", ", anomaliesDetected.Take(2))}");

                _logger.LogInformation("Anomaly alert sent successfully");
            }
            else
            {
                _logger.LogInformation("No anomalies detected in the last hour");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during hourly anomaly detection");
        }
    }
}
