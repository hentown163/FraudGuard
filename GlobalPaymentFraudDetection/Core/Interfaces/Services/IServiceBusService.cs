using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface IServiceBusService
{
    Task SendFraudAlertAsync(FraudAlert alert);
    Task SendTransactionEventAsync(Transaction transaction);
}
