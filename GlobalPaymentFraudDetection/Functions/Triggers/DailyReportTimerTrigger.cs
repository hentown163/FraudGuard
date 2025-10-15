using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Functions.Models;

namespace GlobalPaymentFraudDetection.Functions.Triggers;

public class DailyReportTimerTrigger
{
    private readonly ILogger<DailyReportTimerTrigger> _logger;

    public DailyReportTimerTrigger(ILogger<DailyReportTimerTrigger> logger)
    {
        _logger = logger;
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

            var report = new DailyReportSummary
            {
                ReportDate = reportDate,
                TotalTransactions = await GetTotalTransactions(reportDate, cancellationToken),
                FraudulentTransactions = await GetFraudulentTransactions(reportDate, cancellationToken),
                SuspiciousTransactions = await GetSuspiciousTransactions(reportDate, cancellationToken),
                TotalAmount = await GetTotalAmount(reportDate, cancellationToken),
                FraudAmount = await GetFraudAmount(reportDate, cancellationToken),
                TopRiskFactors = await GetTopRiskFactors(reportDate, cancellationToken),
                GatewayDistribution = await GetGatewayDistribution(reportDate, cancellationToken)
            };

            report.FraudRate = report.TotalTransactions > 0
                ? (double)report.FraudulentTransactions / report.TotalTransactions * 100
                : 0;

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
            var anomalies = await DetectTransactionAnomalies(cancellationToken);

            if (anomalies.Any())
            {
                _logger.LogWarning("Detected {Count} anomalies in the last hour", anomalies.Count);
                await ProcessAnomalies(anomalies, cancellationToken);
            }
            else
            {
                _logger.LogInformation("No anomalies detected in the last hour");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during anomaly detection");
            throw;
        }
    }

    private async Task<int> GetTotalTransactions(DateTime date, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new Random().Next(1000, 5000);
    }

    private async Task<int> GetFraudulentTransactions(DateTime date, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new Random().Next(10, 100);
    }

    private async Task<int> GetSuspiciousTransactions(DateTime date, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new Random().Next(50, 200);
    }

    private async Task<decimal> GetTotalAmount(DateTime date, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new Random().Next(500000, 2000000);
    }

    private async Task<decimal> GetFraudAmount(DateTime date, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new Random().Next(10000, 50000);
    }

    private async Task<List<string>> GetTopRiskFactors(DateTime date, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new List<string>
        {
            "High transaction velocity",
            "Geographic anomaly",
            "Suspicious IP address",
            "Device fingerprint mismatch",
            "Unusual transaction amount"
        };
    }

    private async Task<Dictionary<string, int>> GetGatewayDistribution(DateTime date, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return new Dictionary<string, int>
        {
            { "Stripe", 1200 },
            { "PayPal", 850 },
            { "Braintree", 600 },
            { "Authorize.Net", 450 }
        };
    }

    private async Task<List<string>> DetectTransactionAnomalies(CancellationToken ct)
    {
        await Task.Delay(50, ct);
        return new List<string>();
    }

    private async Task ProcessAnomalies(List<string> anomalies, CancellationToken ct)
    {
        _logger.LogInformation("Processing {Count} anomalies", anomalies.Count);
        await Task.Delay(100, ct);
    }

    private async Task SendReportNotification(DailyReportSummary report, CancellationToken ct)
    {
        _logger.LogInformation("Sending report notification for {Date}", report.ReportDate);
        await Task.Delay(50, ct);
    }
}
