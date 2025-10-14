using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public interface IPaymentGatewayService
{
    Task<PaymentGatewayTransaction> ProcessStripeWebhookAsync(string payload, string signature);
    Task<PaymentGatewayTransaction> ProcessPayPalWebhookAsync(string payload, Dictionary<string, string> headers);
    Task<Transaction> MapToTransactionAsync(PaymentGatewayTransaction gatewayTransaction);
}
