using GlobalPaymentFraudDetection.Models;
using Stripe;
using Newtonsoft.Json;

namespace GlobalPaymentFraudDetection.Services;

public class PaymentGatewayService : IPaymentGatewayService
{
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<PaymentGatewayService> _logger;

    public PaymentGatewayService(IKeyVaultService keyVaultService, ILogger<PaymentGatewayService> logger)
    {
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public async Task<PaymentGatewayTransaction> ProcessStripeWebhookAsync(string payload, string signature)
    {
        var webhookSecret = await _keyVaultService.GetSecretAsync("StripeWebhookSecret");
        
        var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);
        
        if (stripeEvent.Type == Events.ChargeSucceeded || 
            stripeEvent.Type == Events.PaymentIntentSucceeded)
        {
            var charge = stripeEvent.Data.Object as Charge;
            
            return new PaymentGatewayTransaction
            {
                GatewayTransactionId = charge?.Id ?? string.Empty,
                Gateway = "Stripe",
                CustomerId = charge?.CustomerId ?? string.Empty,
                Amount = (charge?.Amount ?? 0) / 100m,
                Currency = charge?.Currency?.ToUpper() ?? "USD",
                Status = charge?.Status ?? "unknown",
                PaymentMethodId = charge?.PaymentMethodId ?? string.Empty,
                PaymentMethodType = charge?.PaymentMethod?.Type ?? "unknown",
                RawData = new Dictionary<string, object> { { "charge", charge ?? new() } },
                CreatedAt = DateTime.UtcNow
            };
        }

        throw new InvalidOperationException($"Unsupported event type: {stripeEvent.Type}");
    }

    public async Task<PaymentGatewayTransaction> ProcessPayPalWebhookAsync(string payload, string webhookId)
    {
        var webhookData = JsonConvert.DeserializeObject<PayPalWebhookEvent>(payload);
        
        if (webhookData == null)
            throw new InvalidOperationException("Invalid PayPal webhook payload");

        if (webhookData.EventType == "PAYMENT.CAPTURE.COMPLETED")
        {
            var resource = webhookData.Resource as dynamic;
            
            return new PaymentGatewayTransaction
            {
                GatewayTransactionId = webhookData.Id,
                Gateway = "PayPal",
                CustomerId = resource?.payer?.payer_id?.ToString() ?? string.Empty,
                Amount = decimal.Parse(resource?.amount?.value?.ToString() ?? "0"),
                Currency = resource?.amount?.currency_code?.ToString() ?? "USD",
                Status = resource?.status?.ToString() ?? "unknown",
                PaymentMethodId = webhookId,
                PaymentMethodType = "paypal",
                RawData = new Dictionary<string, object> { { "event", webhookData } },
                CreatedAt = webhookData.CreateTime
            };
        }

        throw new InvalidOperationException($"Unsupported PayPal event type: {webhookData.EventType}");
    }

    public async Task<Transaction> MapToTransactionAsync(PaymentGatewayTransaction gatewayTransaction)
    {
        return await Task.FromResult(new Transaction
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = gatewayTransaction.CustomerId,
            Amount = gatewayTransaction.Amount,
            Currency = gatewayTransaction.Currency,
            Timestamp = gatewayTransaction.CreatedAt,
            PaymentGateway = gatewayTransaction.Gateway,
            PaymentMethod = gatewayTransaction.PaymentMethodType,
            Status = "PENDING",
            Metadata = new Dictionary<string, string>
            {
                { "GatewayTransactionId", gatewayTransaction.GatewayTransactionId },
                { "PaymentMethodId", gatewayTransaction.PaymentMethodId }
            }
        });
    }
}
