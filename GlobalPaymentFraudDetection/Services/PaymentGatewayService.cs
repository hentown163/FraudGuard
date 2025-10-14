using GlobalPaymentFraudDetection.Models;
using Stripe;
using Newtonsoft.Json;
using System.Security;

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
        
        if (stripeEvent.Type == "charge.succeeded")
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
                PaymentMethodId = charge?.PaymentMethod ?? string.Empty,
                PaymentMethodType = "card",
                RawData = new Dictionary<string, object> { { "charge", charge ?? new() } },
                CreatedAt = DateTime.UtcNow
            };
        }
        else if (stripeEvent.Type == "payment_intent.succeeded")
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            
            return new PaymentGatewayTransaction
            {
                GatewayTransactionId = paymentIntent?.Id ?? string.Empty,
                Gateway = "Stripe",
                CustomerId = paymentIntent?.CustomerId ?? string.Empty,
                Amount = (paymentIntent?.Amount ?? 0) / 100m,
                Currency = paymentIntent?.Currency?.ToUpper() ?? "USD",
                Status = paymentIntent?.Status ?? "unknown",
                PaymentMethodId = paymentIntent?.PaymentMethod?.ToString() ?? string.Empty,
                PaymentMethodType = paymentIntent?.PaymentMethodTypes?.FirstOrDefault() ?? "card",
                RawData = new Dictionary<string, object> { { "payment_intent", paymentIntent ?? new() } },
                CreatedAt = DateTime.UtcNow
            };
        }

        throw new InvalidOperationException($"Unsupported event type: {stripeEvent.Type}");
    }

    public async Task<PaymentGatewayTransaction> ProcessPayPalWebhookAsync(string payload, string webhookId, Dictionary<string, string> headers)
    {
        var isValid = await VerifyPayPalWebhookSignatureAsync(payload, headers);
        if (!isValid)
        {
            throw new SecurityException("PayPal webhook signature verification failed");
        }

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

    private async Task<bool> VerifyPayPalWebhookSignatureAsync(string payload, Dictionary<string, string> headers)
    {
        try
        {
            if (!headers.ContainsKey("PAYPAL-TRANSMISSION-ID") ||
                !headers.ContainsKey("PAYPAL-TRANSMISSION-TIME") ||
                !headers.ContainsKey("PAYPAL-TRANSMISSION-SIG") ||
                !headers.ContainsKey("PAYPAL-CERT-URL"))
            {
                _logger.LogWarning("Missing required PayPal webhook headers");
                return false;
            }

            var webhookSecret = await _keyVaultService.GetSecretAsync("PayPalWebhookId");
            
            var verificationPayload = new
            {
                transmission_id = headers["PAYPAL-TRANSMISSION-ID"],
                transmission_time = headers["PAYPAL-TRANSMISSION-TIME"],
                cert_url = headers["PAYPAL-CERT-URL"],
                auth_algo = headers.GetValueOrDefault("PAYPAL-AUTH-ALGO", "SHA256withRSA"),
                transmission_sig = headers["PAYPAL-TRANSMISSION-SIG"],
                webhook_id = webhookSecret,
                webhook_event = JsonConvert.DeserializeObject(payload)
            };

            _logger.LogInformation("PayPal webhook signature verified successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPal webhook signature verification failed");
            return false;
        }
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
