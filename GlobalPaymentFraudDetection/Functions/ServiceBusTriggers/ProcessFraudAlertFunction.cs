using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using System.Text.Json;

namespace GlobalPaymentFraudDetection.Functions.ServiceBusTriggers;

public class ProcessFraudAlertFunction
{
    private readonly ILogger<ProcessFraudAlertFunction> _logger;
    private readonly IFraudAlertRepository _fraudAlertRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly INotificationService _notificationService;

    public ProcessFraudAlertFunction(
        ILogger<ProcessFraudAlertFunction> logger,
        IFraudAlertRepository fraudAlertRepository,
        ITransactionRepository transactionRepository,
        INotificationService notificationService)
    {
        _logger = logger;
        _fraudAlertRepository = fraudAlertRepository;
        _transactionRepository = transactionRepository;
        _notificationService = notificationService;
    }

    [Function("ProcessFraudAlert")]
    public async Task Run(
        [ServiceBusTrigger("fraud-alerts", Connection = "ServiceBusConnection")] string messageBody)
    {
        _logger.LogInformation($"Processing fraud alert: {messageBody}");

        try
        {
            var alert = JsonSerializer.Deserialize<FraudAlert>(messageBody);
            if (alert == null)
            {
                _logger.LogWarning("Invalid alert message received");
                return;
            }

            await _fraudAlertRepository.CreateAsync(alert);
            _logger.LogInformation($"Alert {alert.Id} saved to database");

            if (alert.Severity == "Critical" || alert.Severity == "High")
            {
                var transaction = await _transactionRepository.GetByIdAsync(alert.TransactionId);
                if (transaction != null)
                {
                    var smsMessage = $"FRAUD ALERT [{alert.Severity}]: Transaction {alert.TransactionId} from user {alert.UserId}. Amount: ${transaction.Amount}. Score: {transaction.FraudScore:F2}";
                    await _notificationService.SendSmsAlertAsync("FRAUD_TEAM", smsMessage);

                    var emailSubject = $"[{alert.Severity}] Fraud Alert - Transaction {alert.TransactionId}";
                    var emailBody = $@"
Fraud Alert Details:
-------------------
Alert ID: {alert.Id}
Severity: {alert.Severity}
Transaction ID: {alert.TransactionId}
User ID: {alert.UserId}
Amount: ${transaction.Amount}
Gateway: {transaction.Gateway}
Fraud Score: {transaction.FraudScore:F2}
Created: {alert.CreatedAt}

Risk Factors:
{string.Join("\n", alert.RiskFactors ?? new List<string> { "None specified" })}

Transaction Details:
IP Address: {transaction.IpAddress}
Device ID: {transaction.DeviceId}
Location: {transaction.Location}

Action Required: Please review this transaction in the dashboard.
                    ";

                    await _notificationService.SendEmailAsync(
                        "fraud-team@example.com",
                        emailSubject,
                        emailBody);

                    _logger.LogInformation($"Notifications sent for {alert.Severity} alert {alert.Id}");
                }
            }

            _logger.LogInformation($"Fraud alert {alert.Id} processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fraud alert");
            throw;
        }
    }
}
