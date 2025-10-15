using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace GlobalPaymentFraudDetection.Core.Services;

public class NotificationService : INotificationService
{
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<NotificationService> _logger;
    private bool _twilioInitialized = false;

    public NotificationService(IKeyVaultService keyVaultService, ILogger<NotificationService> logger)
    {
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    private async Task InitializeTwilioAsync()
    {
        if (!_twilioInitialized)
        {
            var accountSid = await _keyVaultService.GetSecretAsync("TwilioAccountSid");
            var authToken = await _keyVaultService.GetSecretAsync("TwilioAuthToken");
            TwilioClient.Init(accountSid, authToken);
            _twilioInitialized = true;
        }
    }

    public async Task SendSmsAlertAsync(string phoneNumber, FraudAlert alert)
    {
        try
        {
            await InitializeTwilioAsync();
            
            var fromNumber = await _keyVaultService.GetSecretAsync("TwilioPhoneNumber");
            var message = $"FRAUD ALERT: Transaction {alert.TransactionId} flagged. " +
                         $"Amount: ${alert.Amount}, Risk: {alert.FraudProbability:P0}. " +
                         $"Severity: {alert.Severity}";

            var messageResource = await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(fromNumber),
                to: new PhoneNumber(phoneNumber)
            );

            _logger.LogInformation("SMS alert sent to {PhoneNumber}. MessageSid: {MessageSid}", 
                phoneNumber, messageResource.Sid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS alert to {PhoneNumber}", phoneNumber);
        }
    }

    public async Task SendEmailAlertAsync(string email, FraudAlert alert)
    {
        await Task.CompletedTask;
        _logger.LogInformation("Email alert would be sent to {Email} for alert {AlertId}", email, alert.AlertId);
    }
}
