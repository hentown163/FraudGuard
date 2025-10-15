using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;

namespace GlobalPaymentFraudDetection.Functions;

public class ScheduledFraudAnalysisFunction
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ScheduledFraudAnalysisFunction> _logger;

    public ScheduledFraudAnalysisFunction(
        ICosmosDbService cosmosDbService,
        IAzureOpenAIService openAIService,
        INotificationService notificationService,
        ILogger<ScheduledFraudAnalysisFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _openAIService = openAIService;
        _notificationService = notificationService;
        _logger = logger;
    }

    [Function("DailyFraudReport")]
    public async Task GenerateDailyFraudReport([TimerTrigger("0 0 9 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Generating daily fraud report at {Time}", DateTime.UtcNow);

        try
        {
            var yesterday = DateTime.UtcNow.AddDays(-1);
            var transactions = await _cosmosDbService.GetTransactionsByDateRangeAsync(yesterday, DateTime.UtcNow);

            if (transactions.Any())
            {
                var summary = await _openAIService.GenerateFraudSummaryAsync(transactions);
                
                await _notificationService.SendEmailAsync(
                    "fraud-team@company.com",
                    "Daily Fraud Detection Report",
                    $@"<h2>Daily Fraud Report - {DateTime.UtcNow:yyyy-MM-dd}</h2>
                       <p>Total Transactions: {transactions.Count}</p>
                       <p>High Risk Transactions: {transactions.Count(t => t.FraudScore > 0.7)}</p>
                       <hr>
                       <h3>AI Analysis:</h3>
                       <pre>{summary}</pre>"
                );

                _logger.LogInformation("Daily fraud report sent successfully");
            }
            else
            {
                _logger.LogInformation("No transactions to report for yesterday");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily fraud report");
        }
    }

    [Function("HourlyAnomalyDetection")]
    public async Task DetectHourlyAnomalies([TimerTrigger("0 0 * * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Running hourly anomaly detection at {Time}", DateTime.UtcNow);

        try
        {
            var lastHour = DateTime.UtcNow.AddHours(-1);
            var recentTransactions = await _cosmosDbService.GetTransactionsByDateRangeAsync(lastHour, DateTime.UtcNow);

            var highRiskTransactions = recentTransactions.Where(t => t.FraudScore > 0.8).ToList();

            if (highRiskTransactions.Any())
            {
                _logger.LogWarning("Found {Count} high-risk transactions in the last hour", highRiskTransactions.Count);
                
                foreach (var transaction in highRiskTransactions.Take(5))
                {
                    await _notificationService.SendSmsAsync(
                        "+1234567890",
                        $"HIGH RISK: Transaction {transaction.Id} - ${transaction.Amount} - Score: {transaction.FraudScore:P}"
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hourly anomaly detection");
        }
    }
}
