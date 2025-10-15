using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Interfaces.Services;

public interface IPaymentGatewayService
{
    Task<PaymentGatewayTransaction> ProcessStripeWebhookAsync(string payload, string signature);
    Task<PaymentGatewayTransaction> ProcessPayPalWebhookAsync(string payload, Dictionary<string, string> headers);
    Task<PaymentGatewayTransaction> ProcessBraintreeWebhookAsync(string payload, string signature);
    Task<PaymentGatewayTransaction> ProcessAuthorizeNetWebhookAsync(string payload, Dictionary<string, string> headers);
    Task<PaymentGatewayResult> ProcessPaymentWithFailoverAsync(decimal amount, string currency, string customerId, string paymentMethodToken);
    Task<Transaction> MapToTransactionAsync(PaymentGatewayTransaction gatewayTransaction);
}
