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

    public async Task<PaymentGatewayTransaction> ProcessPayPalWebhookAsync(string payload, Dictionary<string, string> headers)
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
            var webhookId = await _keyVaultService.GetSecretAsync("PayPalWebhookId");
            
            return new PaymentGatewayTransaction
            {
                GatewayTransactionId = webhookData.Id,
                Gateway = "PayPal",
                CustomerId = resource?.payer?.payer_id?.ToString() ?? string.Empty,
                Amount = decimal.Parse(resource?.amount?.value?.ToString() ?? "0"),
                Currency = resource?.amount?.currency_code?.ToString() ?? "USD",
                Status = resource?.status?.ToString() ?? "unknown",
                PaymentMethodId = resource?.id?.ToString() ?? webhookData.Id,
                PaymentMethodType = "paypal",
                RawData = new Dictionary<string, object> { 
                    { "event", webhookData },
                    { "webhook_id", webhookId }
                },
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

            var certUrl = headers["PAYPAL-CERT-URL"];
            if (!Uri.TryCreate(certUrl, UriKind.Absolute, out var certUri))
            {
                _logger.LogWarning("PayPal certificate URL is invalid: {CertUrl}", certUrl);
                return false;
            }

            var trustedHosts = new[] { "api.paypal.com", "api-m.paypal.com", "api.sandbox.paypal.com", "api-m.sandbox.paypal.com" };
            if (!trustedHosts.Contains(certUri.Host, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PayPal certificate URL is not from a trusted PayPal domain: {CertUrl}", certUrl);
                return false;
            }

            var authAlgo = headers.GetValueOrDefault("PAYPAL-AUTH-ALGO", "SHA256withRSA");
            if (authAlgo != "SHA256withRSA")
            {
                _logger.LogWarning("Unsupported PayPal auth algorithm: {AuthAlgo}", authAlgo);
                return false;
            }

            var webhookId = await _keyVaultService.GetSecretAsync("PayPalWebhookId");
            var paypalClientId = await _keyVaultService.GetSecretAsync("PayPalClientId");
            var paypalSecret = await _keyVaultService.GetSecretAsync("PayPalSecret");
            var paypalMode = await _keyVaultService.GetSecretAsync("PayPalMode");
            
            var baseUrl = paypalMode?.ToLower() == "live" 
                ? "https://api-m.paypal.com" 
                : "https://api-m.sandbox.paypal.com";

            using var httpClient = new HttpClient();
            
            var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{paypalClientId}:{paypalSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
            
            var tokenResponse = await httpClient.PostAsync(
                $"{baseUrl}/v1/oauth2/token",
                new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") })
            );

            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal OAuth token request failed: {StatusCode}", tokenResponse.StatusCode);
                return false;
            }

            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonConvert.DeserializeObject<Dictionary<string, object>>(tokenContent);
            var accessToken = tokenData?.GetValueOrDefault("access_token")?.ToString();

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("PayPal OAuth token not found in response");
                return false;
            }

            var verificationPayload = new
            {
                transmission_id = headers["PAYPAL-TRANSMISSION-ID"],
                transmission_time = headers["PAYPAL-TRANSMISSION-TIME"],
                cert_url = headers["PAYPAL-CERT-URL"],
                auth_algo = headers.GetValueOrDefault("PAYPAL-AUTH-ALGO", "SHA256withRSA"),
                transmission_sig = headers["PAYPAL-TRANSMISSION-SIG"],
                webhook_id = webhookId,
                webhook_event = JsonConvert.DeserializeObject(payload)
            };

            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var verifyContent = new StringContent(
                JsonConvert.SerializeObject(verificationPayload),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync($"{baseUrl}/v1/notifications/verify-webhook-signature", verifyContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal webhook verification API failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return false;
            }

            var verificationResult = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseContent);
            var verificationStatus = verificationResult?.GetValueOrDefault("verification_status")?.ToString() ?? string.Empty;

            if (verificationStatus != "SUCCESS")
            {
                _logger.LogWarning("PayPal webhook signature verification failed: {Status}", verificationStatus);
                return false;
            }

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
