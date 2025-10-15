using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using Azure.Messaging.ServiceBus;
using System.Text.Json;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Services;

public class ServiceBusService : IServiceBusService
{
    private readonly ServiceBusSender _fraudAlertSender;
    private readonly ServiceBusSender _transactionSender;

    public ServiceBusService(ServiceBusClient client)
    {
        _fraudAlertSender = client.CreateSender("fraud-alerts");
        _transactionSender = client.CreateSender("transaction-events");
    }

    public async Task SendFraudAlertAsync(FraudAlert alert)
    {
        var message = new ServiceBusMessage(JsonSerializer.Serialize(alert))
        {
            ContentType = "application/json",
            MessageId = alert.AlertId
        };
        
        await _fraudAlertSender.SendMessageAsync(message);
    }

    public async Task SendTransactionEventAsync(Transaction transaction)
    {
        var message = new ServiceBusMessage(JsonSerializer.Serialize(transaction))
        {
            ContentType = "application/json",
            MessageId = transaction.TransactionId
        };
        
        await _transactionSender.SendMessageAsync(message);
    }
}
