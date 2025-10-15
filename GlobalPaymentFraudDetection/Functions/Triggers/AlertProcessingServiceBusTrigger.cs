using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using GlobalPaymentFraudDetection.Models;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Functions.Models;

namespace GlobalPaymentFraudDetection.Functions.Triggers;

public class AlertProcessingServiceBusTrigger
{
    private readonly ILogger<AlertProcessingServiceBusTrigger> _logger;
    private readonly IFraudAlertRepository _alertRepository;
    private readonly INotificationService _notificationService;
    private readonly IFraudScoringService _fraudScoringService;
    private readonly ICosmosDbService _cosmosDbService;

    public AlertProcessingServiceBusTrigger(
        ILogger<AlertProcessingServiceBusTrigger> logger,
        IFraudAlertRepository alertRepository,
        INotificationService notificationService,
        IFraudScoringService fraudScoringService,
        ICosmosDbService cosmosDbService)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _notificationService = notificationService;
        _fraudScoringService = fraudScoringService;
        _cosmosDbService = cosmosDbService;
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

            var fraudAlert = MapToFraudAlert(alert);
            
            await ProcessAlertBySeverity(fraudAlert, cancellationToken);

            await _alertRepository.AddAsync(fraudAlert);

            if (alert.Severity == "Critical" || alert.Severity == "High")
            {
                await SendImmediateNotification(fraudAlert, cancellationToken);
            }

            _logger.LogInformation("Alert {AlertId} processed and stored successfully", alert.AlertId);
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

            var tasks = transactions.Select(async r =>
            {
                var transaction = MapToTransaction(r);
                var result = await _fraudScoringService.ScoreTransactionAsync(transaction);
                
                transaction.FraudScore = result.FraudProbability;
                transaction.IsFraudulent = result.IsFraudulent;
                transaction.Status = result.Decision;
                
                await _cosmosDbService.StoreTransactionAsync(transaction);

                return new FraudAnalysisResponse
                {
                    TransactionId = transaction.TransactionId,
                    FraudScore = result.FraudProbability,
                    RiskLevel = result.FraudProbability >= 0.7 ? "Critical" : 
                                result.FraudProbability >= 0.5 ? "High" : 
                                result.FraudProbability >= 0.3 ? "Medium" : "Low",
                    Decision = result.Decision,
                    RiskFactors = result.RiskFactors.Keys.ToList(),
                    AnalyzedAt = result.ProcessedAt
                };
            });

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

    private async Task ProcessAlertBySeverity(FraudAlert alert, CancellationToken ct)
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

    private FraudAlert MapToFraudAlert(AlertMessage message)
    {
        return new FraudAlert
        {
            AlertId = message.AlertId,
            TransactionId = message.TransactionId,
            AlertType = message.AlertType,
            Severity = message.Severity,
            Message = message.Message,
            Reasons = message.Reasons,
            CreatedAt = message.CreatedAt,
            Status = "PENDING",
            IsResolved = false
        };
    }

    private Transaction MapToTransaction(FraudAnalysisRequest request)
    {
        return new Transaction
        {
            TransactionId = request.TransactionId,
            UserId = request.UserEmail,
            Amount = request.Amount,
            Currency = request.Currency,
            IpAddress = request.IpAddress,
            PaymentGateway = request.PaymentGateway,
            DeviceId = request.DeviceFingerprint,
            Timestamp = DateTime.UtcNow,
            Status = "PENDING",
            Metadata = request.Metadata ?? new Dictionary<string, string>()
        };
    }

    private async Task SendImmediateNotification(FraudAlert alert, CancellationToken ct)
    {
        _logger.LogInformation("Sending immediate notification for alert {AlertId}", alert.AlertId);
        
        try
        {
            await _notificationService.SendSmsAlertAsync("+1234567890", alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS notification for alert {AlertId}", alert.AlertId);
        }
    }

    private async Task EscalateAlert(FraudAlert alert, CancellationToken ct)
    {
        _logger.LogInformation("Escalating critical alert {AlertId} to management", alert.AlertId);
        await Task.CompletedTask;
    }

    private async Task NotifySecurityTeam(FraudAlert alert, CancellationToken ct)
    {
        _logger.LogInformation("Notifying security team about alert {AlertId}", alert.AlertId);
        await Task.CompletedTask;
    }

    private async Task QueueForReview(FraudAlert alert, CancellationToken ct)
    {
        _logger.LogInformation("Queueing alert {AlertId} for manual review", alert.AlertId);
        alert.Status = "REVIEW_PENDING";
        await Task.CompletedTask;
    }
}
