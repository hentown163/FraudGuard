using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using System.Text;

namespace GlobalPaymentFraudDetection.Functions.TimerTriggers;

public class DailyFraudReportFunction
{
    private readonly ILogger<DailyFraudReportFunction> _logger;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFraudAlertRepository _fraudAlertRepository;
    private readonly INotificationService _notificationService;

    public DailyFraudReportFunction(
        ILogger<DailyFraudReportFunction> logger,
        ITransactionRepository transactionRepository,
        IFraudAlertRepository fraudAlertRepository,
        INotificationService notificationService)
    {
        _logger = logger;
        _transactionRepository = transactionRepository;
        _fraudAlertRepository = fraudAlertRepository;
        _notificationService = notificationService;
    }

    [Function("DailyFraudReport")]
    public async Task Run([TimerTrigger("0 0 9 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation($"Daily fraud report triggered at: {DateTime.UtcNow}");

        try
        {
            var yesterday = DateTime.UtcNow.AddDays(-1).Date;
            var today = yesterday.AddDays(1);

            var transactions = await _transactionRepository.GetTransactionsByDateRangeAsync(yesterday, today);
            var alerts = await _fraudAlertRepository.GetAlertsByDateRangeAsync(yesterday, today);

            var totalTransactions = transactions.Count();
            var fraudulentTransactions = transactions.Count(t => t.Status == "Declined");
            var pendingReview = transactions.Count(t => t.Status == "Pending");
            var totalAmount = transactions.Sum(t => t.Amount);
            var fraudAmount = transactions.Where(t => t.Status == "Declined").Sum(t => t.Amount);

            var report = new StringBuilder();
            report.AppendLine($"Daily Fraud Detection Report - {yesterday:yyyy-MM-dd}");
            report.AppendLine("=".PadRight(50, '='));
            report.AppendLine();
            report.AppendLine($"Total Transactions: {totalTransactions:N0}");
            report.AppendLine($"Fraudulent: {fraudulentTransactions:N0} ({(totalTransactions > 0 ? fraudulentTransactions * 100.0 / totalTransactions : 0):F2}%)");
            report.AppendLine($"Pending Review: {pendingReview:N0}");
            report.AppendLine();
            report.AppendLine($"Total Transaction Amount: ${totalAmount:N2}");
            report.AppendLine($"Fraud Amount Prevented: ${fraudAmount:N2}");
            report.AppendLine();
            report.AppendLine($"Total Alerts: {alerts.Count()}");
            report.AppendLine($"Critical Alerts: {alerts.Count(a => a.Severity == "Critical")}");
            report.AppendLine($"High Alerts: {alerts.Count(a => a.Severity == "High")}");
            report.AppendLine();

            var topRiskFactors = transactions
                .Where(t => t.RiskFactors != null && t.RiskFactors.Any())
                .SelectMany(t => t.RiskFactors!)
                .GroupBy(r => r)
                .OrderByDescending(g => g.Count())
                .Take(5);

            report.AppendLine("Top Risk Factors:");
            foreach (var factor in topRiskFactors)
            {
                report.AppendLine($"  - {factor.Key}: {factor.Count()} occurrences");
            }

            var reportContent = report.ToString();
            _logger.LogInformation(reportContent);

            await _notificationService.SendEmailAsync(
                "fraud-team@example.com",
                $"Daily Fraud Report - {yesterday:yyyy-MM-dd}",
                reportContent);

            _logger.LogInformation("Daily fraud report sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily fraud report");
        }
    }
}
