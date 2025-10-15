using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Functions.Models;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;

namespace GlobalPaymentFraudDetection.Functions.Triggers;

public class DailyReportTimerTrigger
{
    private readonly ILogger<DailyReportTimerTrigger> _logger;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFraudAlertRepository _alertRepository;
    private readonly INotificationService _notificationService;

    public DailyReportTimerTrigger(
        ILogger<DailyReportTimerTrigger> logger,
        ITransactionRepository transactionRepository,
        IFraudAlertRepository alertRepository,
        INotificationService notificationService)
    {
        _logger = logger;
        _transactionRepository = transactionRepository;
        _alertRepository = alertRepository;
        _notificationService = notificationService;
    }

    [Function("GenerateDailyFraudReport")]
    public async Task GenerateDailyReport(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timer,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Daily fraud report generation started at {Time}", DateTime.UtcNow);

        try
        {
            var reportDate = DateTime.UtcNow.Date.AddDays(-1);
            var endDate = reportDate.AddDays(1);

            var allTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(
                reportDate, endDate);

            var fraudulentTransactions = allTransactions.Where(t => t.IsFraudulent).ToList();
            var suspiciousTransactions = allTransactions
                .Where(t => t.FraudScore >= 0.5 && !t.IsFraudulent).ToList();

            var totalAmount = allTransactions.Sum(t => t.Amount);
            var fraudAmount = fraudulentTransactions.Sum(t => t.Amount);

            var gatewayDistribution = allTransactions
                .GroupBy(t => t.PaymentGateway)
                .ToDictionary(g => g.Key, g => g.Count());

            var topRiskFactors = fraudulentTransactions
                .SelectMany(t => t.Status == "DECLINED" ? new[] { "High Fraud Score" } : Array.Empty<string>())
                .GroupBy(f => f)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            var report = new DailyReportSummary
            {
                ReportDate = reportDate,
                TotalTransactions = allTransactions.Count,
                FraudulentTransactions = fraudulentTransactions.Count,
                SuspiciousTransactions = suspiciousTransactions.Count,
                TotalAmount = totalAmount,
                FraudAmount = fraudAmount,
                FraudRate = allTransactions.Count > 0
                    ? (double)fraudulentTransactions.Count / allTransactions.Count * 100
                    : 0,
                TopRiskFactors = topRiskFactors.Any() ? topRiskFactors : new List<string> { "No fraud detected" },
                GatewayDistribution = gatewayDistribution
            };

            _logger.LogInformation(
                "Daily report generated: Date={ReportDate}, Total={Total}, Fraud={Fraud}, Rate={Rate:F2}%",
                report.ReportDate, report.TotalTransactions, report.FraudulentTransactions, report.FraudRate);

            await SendReportNotification(report, cancellationToken);

            if (timer.ScheduleStatus is not null)
            {
                _logger.LogInformation("Next report scheduled at {NextRun}", timer.ScheduleStatus.Next);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily fraud report");
            throw;
        }
    }

    [Function("HourlyAnomalyDetection")]
    public async Task DetectAnomalies(
        [TimerTrigger("0 0 * * * *")] TimerInfo timer,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hourly anomaly detection started at {Time}", DateTime.UtcNow);

        try
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var recentTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(
                oneHourAgo, DateTime.UtcNow);

            var anomalies = new List<string>();

            var avgAmount = recentTransactions.Any() 
                ? recentTransactions.Average(t => (double)t.Amount) 
                : 0;
            var highValueTransactions = recentTransactions
                .Where(t => (double)t.Amount > avgAmount * 3)
                .ToList();

            if (highValueTransactions.Any())
            {
                anomalies.Add($"Detected {highValueTransactions.Count} high-value transactions (>3x average)");
                _logger.LogWarning("Anomaly: {Count} high-value transactions detected", highValueTransactions.Count);
            }

            var suspiciousScoreTransactions = recentTransactions
                .Where(t => t.FraudScore >= 0.7)
                .ToList();

            if (suspiciousScoreTransactions.Count > recentTransactions.Count * 0.1)
            {
                anomalies.Add($"High fraud score rate: {suspiciousScoreTransactions.Count} transactions");
                _logger.LogWarning("Anomaly: {Count} transactions with high fraud scores", suspiciousScoreTransactions.Count);
            }

            var failedTransactions = recentTransactions
                .Where(t => t.Status == "DECLINED")
                .ToList();

            if (failedTransactions.Count > recentTransactions.Count * 0.3)
            {
                anomalies.Add($"High decline rate: {failedTransactions.Count} declined transactions");
                _logger.LogWarning("Anomaly: High decline rate of {Rate:F1}%", 
                    (double)failedTransactions.Count / recentTransactions.Count * 100);
            }

            if (anomalies.Any())
            {
                _logger.LogWarning("Detected {Count} anomalies in the last hour", anomalies.Count);
                await ProcessAnomalies(anomalies, cancellationToken);
            }
            else
            {
                _logger.LogInformation("No anomalies detected in the last hour ({Count} transactions analyzed)", 
                    recentTransactions.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during anomaly detection");
            throw;
        }
    }

    private async Task ProcessAnomalies(List<string> anomalies, CancellationToken ct)
    {
        _logger.LogInformation("Processing {Count} anomalies for notification", anomalies.Count);
        
        foreach (var anomaly in anomalies)
        {
            _logger.LogWarning("Anomaly detected: {Anomaly}", anomaly);
        }
    }

    private async Task SendReportNotification(DailyReportSummary report, CancellationToken ct)
    {
        _logger.LogInformation(
            "Daily report summary: {Total} transactions, {Fraud} fraudulent ({Rate:F2}% fraud rate), ${Amount:N2} total volume",
            report.TotalTransactions, report.FraudulentTransactions, report.FraudRate, report.TotalAmount);
    }
}
