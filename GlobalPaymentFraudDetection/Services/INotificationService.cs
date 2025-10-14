using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface INotificationService
{
    Task SendSmsAlertAsync(string phoneNumber, FraudAlert alert);
    Task SendEmailAlertAsync(string email, FraudAlert alert);
}
