using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface IServiceBusService
{
    Task SendFraudAlertAsync(FraudAlert alert);
    Task SendTransactionEventAsync(Transaction transaction);
}
