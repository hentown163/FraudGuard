using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Controllers;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Hubs;
using GlobalPaymentFraudDetection.Models;
using System.Text;

namespace GlobalPaymentFraudDetection.Tests.Controllers;

public class WebhookControllerTests
{
    private readonly Mock<IPaymentGatewayService> _paymentGatewayServiceMock;
    private readonly Mock<IFraudScoringService> _fraudScoringServiceMock;
    private readonly Mock<ICosmosDbService> _cosmosDbServiceMock;
    private readonly Mock<IHubContext<FraudDetectionHub>> _hubContextMock;
    private readonly Mock<ILogger<WebhookController>> _loggerMock;
    private readonly WebhookController _controller;

    public WebhookControllerTests()
    {
        _paymentGatewayServiceMock = new Mock<IPaymentGatewayService>();
        _fraudScoringServiceMock = new Mock<IFraudScoringService>();
        _cosmosDbServiceMock = new Mock<ICosmosDbService>();
        _hubContextMock = new Mock<IHubContext<FraudDetectionHub>>();
        _loggerMock = new Mock<ILogger<WebhookController>>();

        _controller = new WebhookController(
            _paymentGatewayServiceMock.Object,
            _fraudScoringServiceMock.Object,
            _cosmosDbServiceMock.Object,
            _hubContextMock.Object,
            _loggerMock.Object
        );

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        _hubContextMock.Setup(x => x.Clients).Returns(mockClients.Object);
        mockClients.Setup(x => x.All).Returns(mockClientProxy.Object);
    }

    [Fact]
    public async Task StripeWebhook_WithValidPayload_ReturnsOkResult()
    {
        var payload = "{\"type\": \"charge.succeeded\"}";
        var signature = "test-signature";

        var gatewayTransaction = new PaymentGatewayTransaction
        {
            GatewayTransactionId = "stripe_123",
            Gateway = "Stripe",
            Amount = 100m
        };

        var transaction = new Transaction
        {
            TransactionId = "TXN123",
            Amount = 100m
        };

        var fraudResponse = new FraudScoreResponse
        {
            TransactionId = "TXN123",
            FraudProbability = 0.3
        };

        _paymentGatewayServiceMock
            .Setup(x => x.ProcessStripeWebhookAsync(payload, signature))
            .ReturnsAsync(gatewayTransaction);

        _paymentGatewayServiceMock
            .Setup(x => x.MapToTransactionAsync(gatewayTransaction))
            .ReturnsAsync(transaction);

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ReturnsAsync(fraudResponse);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        httpContext.Request.Headers["Stripe-Signature"] = signature;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.StripeWebhook();

        result.Should().BeOfType<OkObjectResult>();
        _cosmosDbServiceMock.Verify(x => x.StoreTransactionAsync(transaction), Times.Once);
    }

    [Fact]
    public async Task StripeWebhook_WhenProcessingFails_ReturnsBadRequest()
    {
        var payload = "{\"type\": \"charge.succeeded\"}";
        var signature = "invalid-signature";

        _paymentGatewayServiceMock
            .Setup(x => x.ProcessStripeWebhookAsync(payload, signature))
            .ThrowsAsync(new Exception("Invalid signature"));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        httpContext.Request.Headers["Stripe-Signature"] = signature;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.StripeWebhook();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PayPalWebhook_WithValidPayload_ReturnsOkResult()
    {
        var payload = "{\"event_type\": \"PAYMENT.CAPTURE.COMPLETED\"}";

        var gatewayTransaction = new PaymentGatewayTransaction
        {
            GatewayTransactionId = "paypal_123",
            Gateway = "PayPal",
            Amount = 150m
        };

        var transaction = new Transaction
        {
            TransactionId = "TXN456",
            Amount = 150m
        };

        var fraudResponse = new FraudScoreResponse
        {
            TransactionId = "TXN456",
            FraudProbability = 0.2
        };

        _paymentGatewayServiceMock
            .Setup(x => x.ProcessPayPalWebhookAsync(payload, It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync(gatewayTransaction);

        _paymentGatewayServiceMock
            .Setup(x => x.MapToTransactionAsync(gatewayTransaction))
            .ReturnsAsync(transaction);

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ReturnsAsync(fraudResponse);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.PayPalWebhook();

        result.Should().BeOfType<OkObjectResult>();
        _cosmosDbServiceMock.Verify(x => x.StoreTransactionAsync(transaction), Times.Once);
    }
}
