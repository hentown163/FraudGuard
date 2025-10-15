using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using Stripe;
using Newtonsoft.Json;
using System.Security;
using BraintreeGateway = Braintree.BraintreeGateway;
using BraintreeTransaction = Braintree.Transaction;
using BraintreeEnvironment = Braintree.Environment;
using WebhookKind = Braintree.WebhookKind;
using TransactionRequest = Braintree.TransactionRequest;
using TransactionOptionsRequest = Braintree.TransactionOptionsRequest;
using AuthorizeNet.Api.Controllers;
using AuthorizeNet.Api.Contracts.V1;
using AuthorizeNet.Api.Controllers.Bases;

namespace GlobalPaymentFraudDetection.Core.Services;

public class PaymentGatewayService : IPaymentGatewayService
{
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<PaymentGatewayService> _logger;
    private readonly IConfiguration _configuration;
    private readonly List<PaymentGatewayType> _gatewayPriority;

    public PaymentGatewayService(IKeyVaultService keyVaultService, ILogger<PaymentGatewayService> logger, IConfiguration configuration)
    {
        _keyVaultService = keyVaultService;
        _logger = logger;
        _configuration = configuration;
        
        var primaryGateway = _configuration["PaymentGateway:PrimaryGateway"] ?? "Braintree";
        var failoverGateways = _configuration.GetSection("PaymentGateway:FailoverGateways").Get<string[]>() ?? Array.Empty<string>();
        
        _gatewayPriority = new List<PaymentGatewayType> { Enum.Parse<PaymentGatewayType>(primaryGateway) };
        foreach (var gateway in failoverGateways)
        {
            if (Enum.TryParse<PaymentGatewayType>(gateway, out var gatewayType))
            {
                _gatewayPriority.Add(gatewayType);
            }
        }
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

    public async Task<PaymentGatewayTransaction> ProcessBraintreeWebhookAsync(string payload, string signature)
    {
        var braintreeGateway = await GetBraintreeGatewayAsync();
        
        var notification = braintreeGateway.WebhookNotification.Parse(signature, payload);
        
        if (notification.Kind == WebhookKind.SUBSCRIPTION_CHARGED_SUCCESSFULLY ||
            notification.Kind == WebhookKind.TRANSACTION_SETTLED)
        {
            var transaction = notification.Transaction;
            
            return new PaymentGatewayTransaction
            {
                GatewayTransactionId = transaction.Id,
                Gateway = "Braintree",
                GatewayType = PaymentGatewayType.Braintree,
                CustomerId = transaction.CustomerDetails?.Id ?? string.Empty,
                Amount = transaction.Amount ?? 0,
                Currency = transaction.CurrencyIsoCode ?? "USD",
                Status = transaction.Status.ToString(),
                PaymentMethodId = transaction.CreditCard?.Token ?? string.Empty,
                PaymentMethodType = transaction.PaymentInstrumentType.ToString(),
                RawData = new Dictionary<string, object> { { "transaction", transaction } },
                CreatedAt = transaction.CreatedAt ?? DateTime.UtcNow
            };
        }

        throw new InvalidOperationException($"Unsupported Braintree webhook kind: {notification.Kind}");
    }

    public async Task<PaymentGatewayTransaction> ProcessAuthorizeNetWebhookAsync(string payload, Dictionary<string, string> headers)
    {
        var webhookData = JsonConvert.DeserializeObject<AuthorizeNetWebhookEvent>(payload);
        
        if (webhookData == null)
            throw new InvalidOperationException("Invalid Authorize.Net webhook payload");

        if (webhookData.EventType == "net.authorize.payment.authorization.created")
        {
            dynamic payloadData = webhookData.Payload;
            
            return new PaymentGatewayTransaction
            {
                GatewayTransactionId = webhookData.NotificationId,
                Gateway = "AuthorizeNet",
                GatewayType = PaymentGatewayType.AuthorizeNet,
                CustomerId = payloadData?.customerProfileId?.ToString() ?? string.Empty,
                Amount = decimal.Parse(payloadData?.authAmount?.ToString() ?? "0"),
                Currency = "USD",
                Status = payloadData?.responseCode?.ToString() == "1" ? "approved" : "declined",
                PaymentMethodId = payloadData?.paymentProfile?.ToString() ?? string.Empty,
                PaymentMethodType = "credit_card",
                RawData = new Dictionary<string, object> { { "event", webhookData } },
                CreatedAt = webhookData.EventDate
            };
        }

        throw new InvalidOperationException($"Unsupported Authorize.Net event type: {webhookData.EventType}");
    }

    public async Task<PaymentGatewayResult> ProcessPaymentWithFailoverAsync(
        decimal amount, 
        string currency, 
        string customerId, 
        string paymentMethodToken)
    {
        var enableFailover = _configuration.GetValue<bool>("PaymentGateway:EnableFailover", true);
        
        foreach (var gateway in _gatewayPriority)
        {
            try
            {
                _logger.LogInformation("Attempting payment with gateway: {Gateway}", gateway);
                
                var result = gateway switch
                {
                    PaymentGatewayType.Braintree => await ProcessBraintreePaymentAsync(amount, currency, customerId, paymentMethodToken),
                    PaymentGatewayType.AuthorizeNet => await ProcessAuthorizeNetPaymentAsync(amount, currency, customerId, paymentMethodToken),
                    PaymentGatewayType.Stripe => await ProcessStripePaymentAsync(amount, currency, customerId, paymentMethodToken),
                    _ => throw new NotImplementedException($"Gateway {gateway} not implemented")
                };

                if (result.Success)
                {
                    _logger.LogInformation("Payment successful with gateway: {Gateway}", gateway);
                    return result;
                }
                
                _logger.LogWarning("Payment failed with gateway {Gateway}: {Error}", gateway, result.ErrorMessage);
                
                if (!enableFailover || _gatewayPriority.Last() == gateway)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment with gateway: {Gateway}", gateway);
                
                if (!enableFailover || _gatewayPriority.Last() == gateway)
                {
                    return new PaymentGatewayResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        GatewayUsed = gateway
                    };
                }
            }
        }

        return new PaymentGatewayResult
        {
            Success = false,
            ErrorMessage = "All payment gateways failed",
            GatewayUsed = _gatewayPriority.First()
        };
    }

    private async Task<PaymentGatewayResult> ProcessBraintreePaymentAsync(
        decimal amount, 
        string currency, 
        string customerId, 
        string paymentMethodToken)
    {
        var gateway = await GetBraintreeGatewayAsync();
        
        var request = new TransactionRequest
        {
            Amount = amount,
            PaymentMethodToken = paymentMethodToken,
            CustomerId = customerId,
            Options = new TransactionOptionsRequest
            {
                SubmitForSettlement = true
            }
        };

        var result = gateway.Transaction.Sale(request);
        
        if (result.IsSuccess())
        {
            return new PaymentGatewayResult
            {
                Success = true,
                TransactionId = result.Target.Id,
                GatewayUsed = PaymentGatewayType.Braintree,
                Transaction = new PaymentGatewayTransaction
                {
                    GatewayTransactionId = result.Target.Id,
                    Gateway = "Braintree",
                    GatewayType = PaymentGatewayType.Braintree,
                    CustomerId = customerId,
                    Amount = amount,
                    Currency = currency,
                    Status = result.Target.Status.ToString(),
                    PaymentMethodId = paymentMethodToken,
                    PaymentMethodType = result.Target.PaymentInstrumentType.ToString(),
                    CreatedAt = DateTime.UtcNow
                }
            };
        }

        return new PaymentGatewayResult
        {
            Success = false,
            ErrorMessage = result.Message,
            GatewayUsed = PaymentGatewayType.Braintree
        };
    }

    private async Task<PaymentGatewayResult> ProcessAuthorizeNetPaymentAsync(
        decimal amount, 
        string currency, 
        string customerId, 
        string paymentMethodToken)
    {
        var apiLoginId = _configuration["AuthorizeNet:ApiLoginId"] ?? 
                         Environment.GetEnvironmentVariable("AUTHORIZENET_API_LOGIN_ID") ?? string.Empty;
        var transactionKey = _configuration["AuthorizeNet:TransactionKey"] ?? 
                             Environment.GetEnvironmentVariable("AUTHORIZENET_TRANSACTION_KEY") ?? string.Empty;
        var environment = _configuration["AuthorizeNet:Environment"] ?? "sandbox";
        
        ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = 
            environment == "production" ? AuthorizeNet.Environment.PRODUCTION : AuthorizeNet.Environment.SANDBOX;

        var merchantAuthentication = new merchantAuthenticationType
        {
            name = apiLoginId,
            ItemElementName = ItemChoiceType.transactionKey,
            Item = transactionKey
        };

        var creditCard = new creditCardType
        {
            cardNumber = paymentMethodToken.Split(':')[0],
            expirationDate = paymentMethodToken.Split(':').Length > 1 ? paymentMethodToken.Split(':')[1] : "1225"
        };

        var paymentType = new paymentType { Item = creditCard };

        var transactionRequest = new transactionRequestType
        {
            transactionType = transactionTypeEnum.authCaptureTransaction.ToString(),
            amount = amount,
            payment = paymentType,
            customer = new customerDataType { id = customerId }
        };

        var request = new createTransactionRequest { transactionRequest = transactionRequest };
        request.merchantAuthentication = merchantAuthentication;

        var controller = new createTransactionController(request);
        controller.Execute();

        var response = controller.GetApiResponse();

        if (response != null && response.transactionResponse != null)
        {
            if (response.transactionResponse.responseCode == "1")
            {
                return new PaymentGatewayResult
                {
                    Success = true,
                    TransactionId = response.transactionResponse.transId,
                    GatewayUsed = PaymentGatewayType.AuthorizeNet,
                    Transaction = new PaymentGatewayTransaction
                    {
                        GatewayTransactionId = response.transactionResponse.transId,
                        Gateway = "AuthorizeNet",
                        GatewayType = PaymentGatewayType.AuthorizeNet,
                        CustomerId = customerId,
                        Amount = amount,
                        Currency = currency,
                        Status = "approved",
                        PaymentMethodId = paymentMethodToken,
                        PaymentMethodType = "credit_card",
                        CreatedAt = DateTime.UtcNow
                    }
                };
            }

            var errorMessages = response.transactionResponse.errors?.Select(e => e.errorText).ToList() ?? new List<string>();
            return new PaymentGatewayResult
            {
                Success = false,
                ErrorMessage = string.Join(", ", errorMessages),
                GatewayUsed = PaymentGatewayType.AuthorizeNet
            };
        }

        return new PaymentGatewayResult
        {
            Success = false,
            ErrorMessage = response?.messages?.message?.FirstOrDefault()?.text ?? "Unknown error",
            GatewayUsed = PaymentGatewayType.AuthorizeNet
        };
    }

    private async Task<PaymentGatewayResult> ProcessStripePaymentAsync(
        decimal amount, 
        string currency, 
        string customerId, 
        string paymentMethodToken)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100),
                Currency = currency.ToLower(),
                Customer = customerId,
                PaymentMethod = paymentMethodToken,
                Confirm = true,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                }
            };

            var service = new PaymentIntentService();
            var paymentIntent = await service.CreateAsync(options);

            if (paymentIntent.Status == "succeeded")
            {
                return new PaymentGatewayResult
                {
                    Success = true,
                    TransactionId = paymentIntent.Id,
                    GatewayUsed = PaymentGatewayType.Stripe,
                    Transaction = new PaymentGatewayTransaction
                    {
                        GatewayTransactionId = paymentIntent.Id,
                        Gateway = "Stripe",
                        GatewayType = PaymentGatewayType.Stripe,
                        CustomerId = customerId,
                        Amount = amount,
                        Currency = currency,
                        Status = paymentIntent.Status,
                        PaymentMethodId = paymentMethodToken,
                        PaymentMethodType = "card",
                        CreatedAt = DateTime.UtcNow
                    }
                };
            }

            return new PaymentGatewayResult
            {
                Success = false,
                ErrorMessage = $"Payment intent status: {paymentIntent.Status}",
                GatewayUsed = PaymentGatewayType.Stripe
            };
        }
        catch (StripeException ex)
        {
            return new PaymentGatewayResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                GatewayUsed = PaymentGatewayType.Stripe
            };
        }
    }

    private async Task<BraintreeGateway> GetBraintreeGatewayAsync()
    {
        var environment = _configuration["Braintree:Environment"] ?? "sandbox";
        var merchantId = _configuration["Braintree:MerchantId"] ?? 
                         Environment.GetEnvironmentVariable("BRAINTREE_MERCHANT_ID") ?? string.Empty;
        var publicKey = _configuration["Braintree:PublicKey"] ?? 
                        Environment.GetEnvironmentVariable("BRAINTREE_PUBLIC_KEY") ?? string.Empty;
        var privateKey = _configuration["Braintree:PrivateKey"] ?? 
                         Environment.GetEnvironmentVariable("BRAINTREE_PRIVATE_KEY") ?? string.Empty;

        var btEnvironment = environment == "production" 
            ? BraintreeEnvironment.PRODUCTION 
            : BraintreeEnvironment.SANDBOX;

        return await Task.FromResult(new BraintreeGateway
        {
            Environment = btEnvironment,
            MerchantId = merchantId,
            PublicKey = publicKey,
            PrivateKey = privateKey
        });
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
