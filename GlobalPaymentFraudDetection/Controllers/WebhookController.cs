using Microsoft.AspNetCore.Mvc;
using GlobalPaymentFraudDetection.Services;
using GlobalPaymentFraudDetection.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace GlobalPaymentFraudDetection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IPaymentGatewayService _paymentGatewayService;
    private readonly IFraudScoringService _fraudScoringService;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IHubContext<FraudDetectionHub> _hubContext;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IPaymentGatewayService paymentGatewayService,
        IFraudScoringService fraudScoringService,
        ICosmosDbService cosmosDbService,
        IHubContext<FraudDetectionHub> hubContext,
        ILogger<WebhookController> logger)
    {
        _paymentGatewayService = paymentGatewayService;
        _fraudScoringService = fraudScoringService;
        _cosmosDbService = cosmosDbService;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpPost("stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        try
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();

            var gatewayTransaction = await _paymentGatewayService.ProcessStripeWebhookAsync(json, signature);
            var transaction = await _paymentGatewayService.MapToTransactionAsync(gatewayTransaction);

            await _cosmosDbService.StoreTransactionAsync(transaction);

            await _hubContext.Clients.All.SendAsync("ReceiveTransactionUpdate", transaction);

            var fraudScore = await _fraudScoringService.ScoreTransactionAsync(transaction);

            await _hubContext.Clients.All.SendAsync("ReceiveScoreUpdate", fraudScore);

            _logger.LogInformation("Stripe webhook processed and persisted: {TransactionId}", transaction.TransactionId);

            return Ok(new { received = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Stripe webhook");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("paypal")]
    public async Task<IActionResult> PayPalWebhook()
    {
        try
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var webhookId = Request.Headers["PAYPAL-TRANSMISSION-ID"].ToString();
            
            var headers = new Dictionary<string, string>();
            foreach (var header in Request.Headers)
            {
                if (header.Key.StartsWith("PAYPAL-", StringComparison.OrdinalIgnoreCase))
                {
                    headers[header.Key] = header.Value.ToString();
                }
            }

            var gatewayTransaction = await _paymentGatewayService.ProcessPayPalWebhookAsync(json, webhookId, headers);
            var transaction = await _paymentGatewayService.MapToTransactionAsync(gatewayTransaction);

            await _cosmosDbService.StoreTransactionAsync(transaction);

            await _hubContext.Clients.All.SendAsync("ReceiveTransactionUpdate", transaction);

            var fraudScore = await _fraudScoringService.ScoreTransactionAsync(transaction);

            await _hubContext.Clients.All.SendAsync("ReceiveScoreUpdate", fraudScore);

            _logger.LogInformation("PayPal webhook processed and persisted: {TransactionId}", transaction.TransactionId);

            return Ok(new { received = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayPal webhook");
            return BadRequest(new { error = ex.Message });
        }
    }
}
