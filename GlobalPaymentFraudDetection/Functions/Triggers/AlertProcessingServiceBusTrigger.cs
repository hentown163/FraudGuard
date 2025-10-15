using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using GlobalPaymentFraudDetection.Functions.Models;

namespace GlobalPaymentFraudDetection.Functions.Triggers;

public class AlertProcessingServiceBusTrigger
{
    private readonly ILogger<AlertProcessingServiceBusTrigger> _logger;

    public AlertProcessingServiceBusTrigger(ILogger<AlertProcessingServiceBusTrigger> logger)
    {
        _logger = logger;
    }

    [Function("ProcessFraudAlert")]
    public async Task ProcessAlert(
        [ServiceBusTrigger("fraud-alerts", Connection = "ServiceBusConnectionString")]
        string alertMessage,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing fraud alert from Service Bus");

        try
        {
            var alert = JsonSerializer.Deserialize<AlertMessage>(alertMessage,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (alert == null)
            {
                _logger.LogWarning("Received invalid alert message");
                return;
            }

            _logger.LogInformation(
                "Alert received: AlertId={AlertId}, Type={AlertType}, Severity={Severity}, TransactionId={TransactionId}",
                alert.AlertId, alert.AlertType, alert.Severity, alert.TransactionId);

            await ProcessAlertBySeverity(alert, cancellationToken);

            await StoreAlertInDatabase(alert, cancellationToken);

            if (alert.Severity == "Critical" || alert.Severity == "High")
            {
                await SendImmediateNotification(alert, cancellationToken);
            }

            _logger.LogInformation("Alert {AlertId} processed successfully", alert.AlertId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fraud alert");
            throw;
        }
    }

    [Function("ProcessBatchTransactions")]
    public async Task ProcessBatch(
        [ServiceBusTrigger("transaction-batch", Connection = "ServiceBusConnectionString")]
        string batchMessage,
        FunctionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing batch transactions from Service Bus");

        try
        {
            var transactions = JsonSerializer.Deserialize<List<FraudAnalysisRequest>>(batchMessage,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (transactions == null || !transactions.Any())
            {
                _logger.LogWarning("Received empty batch");
                return;
            }

            _logger.LogInformation("Processing batch of {Count} transactions", transactions.Count);

            var tasks = transactions.Select(t => AnalyzeTransaction(t, cancellationToken));
            var results = await Task.WhenAll(tasks);

            var highRiskCount = results.Count(r => r.RiskLevel == "High" || r.RiskLevel == "Critical");

            _logger.LogInformation(
                "Batch processed: Total={Total}, HighRisk={HighRisk}",
                transactions.Count, highRiskCount);

            if (highRiskCount > transactions.Count * 0.3)
            {
                _logger.LogWarning("High percentage of risky transactions in batch: {Percentage:F1}%",
                    (double)highRiskCount / transactions.Count * 100);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch transactions");
            throw;
        }
    }

    private async Task ProcessAlertBySeverity(AlertMessage alert, CancellationToken ct)
    {
        switch (alert.Severity)
        {
            case "Critical":
                _logger.LogCritical("CRITICAL ALERT: {AlertId} - {Message}", alert.AlertId, alert.Message);
                await EscalateAlert(alert, ct);
                break;
            case "High":
                _logger.LogError("HIGH PRIORITY ALERT: {AlertId} - {Message}", alert.AlertId, alert.Message);
                await NotifySecurityTeam(alert, ct);
                break;
            case "Medium":
                _logger.LogWarning("MEDIUM ALERT: {AlertId} - {Message}", alert.AlertId, alert.Message);
                await QueueForReview(alert, ct);
                break;
            default:
                _logger.LogInformation("LOW ALERT: {AlertId} - {Message}", alert.AlertId, alert.Message);
                break;
        }
    }

    private async Task<FraudAnalysisResponse> AnalyzeTransaction(FraudAnalysisRequest request, CancellationToken ct)
    {
        await Task.Delay(10, ct);

        var score = CalculateScore(request);
        return new FraudAnalysisResponse
        {
            TransactionId = request.TransactionId,
            FraudScore = score,
            RiskLevel = score >= 0.7 ? "Critical" : score >= 0.5 ? "High" : score >= 0.3 ? "Medium" : "Low",
            Decision = score >= 0.7 ? "Decline" : score >= 0.5 ? "Manual Review" : "Approve",
            RiskFactors = new List<string> { "Analyzed in batch" },
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private double CalculateScore(FraudAnalysisRequest request)
    {
        double score = 0.0;
        if (request.Amount > 1000) score += 0.3;
        if (string.IsNullOrEmpty(request.DeviceFingerprint)) score += 0.2;
        return Math.Min(score, 1.0);
    }

    private async Task StoreAlertInDatabase(AlertMessage alert, CancellationToken ct)
    {
        _logger.LogDebug("Storing alert {AlertId} in database", alert.AlertId);
        await Task.Delay(20, ct);
    }

    private async Task SendImmediateNotification(AlertMessage alert, CancellationToken ct)
    {
        _logger.LogInformation("Sending immediate notification for alert {AlertId}", alert.AlertId);
        await Task.Delay(30, ct);
    }

    private async Task EscalateAlert(AlertMessage alert, CancellationToken ct)
    {
        _logger.LogInformation("Escalating critical alert {AlertId}", alert.AlertId);
        await Task.Delay(50, ct);
    }

    private async Task NotifySecurityTeam(AlertMessage alert, CancellationToken ct)
    {
        _logger.LogInformation("Notifying security team about alert {AlertId}", alert.AlertId);
        await Task.Delay(40, ct);
    }

    private async Task QueueForReview(AlertMessage alert, CancellationToken ct)
    {
        _logger.LogInformation("Queueing alert {AlertId} for review", alert.AlertId);
        await Task.Delay(30, ct);
    }
}
