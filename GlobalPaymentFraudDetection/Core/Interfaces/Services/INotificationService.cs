using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface INotificationService
{
    Task SendSmsAlertAsync(string phoneNumber, FraudAlert alert);
    Task SendEmailAlertAsync(string email, FraudAlert alert);
}
