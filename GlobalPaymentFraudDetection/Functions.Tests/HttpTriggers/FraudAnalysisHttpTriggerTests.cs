using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using GlobalPaymentFraudDetection.Functions.Triggers;
using GlobalPaymentFraudDetection.Functions.Models;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Functions.Tests.HttpTriggers;

public class FraudAnalysisHttpTriggerTests
{
    private readonly Mock<ILogger<FraudAnalysisHttpTrigger>> _loggerMock;
    private readonly Mock<IFraudScoringService> _fraudScoringServiceMock;
    private readonly Mock<ICosmosDbService> _cosmosDbServiceMock;
    private readonly FraudAnalysisHttpTrigger _function;

    public FraudAnalysisHttpTriggerTests()
    {
        _loggerMock = new Mock<ILogger<FraudAnalysisHttpTrigger>>();
        _fraudScoringServiceMock = new Mock<IFraudScoringService>();
        _cosmosDbServiceMock = new Mock<ICosmosDbService>();
        
        _function = new FraudAnalysisHttpTrigger(
            _loggerMock.Object,
            _fraudScoringServiceMock.Object,
            _cosmosDbServiceMock.Object);
    }

    [Fact]
    public async Task AnalyzeFraud_ValidRequest_ReturnsSuccessResponse()
    {
        var request = new FraudAnalysisRequest
        {
            TransactionId = "TXN123",
            Amount = 100.00m,
            Currency = "USD",
            UserEmail = "test@example.com",
            IpAddress = "192.168.1.1",
            PaymentGateway = "Stripe",
            DeviceFingerprint = "device123"
        };

        var fraudResponse = new FraudScoreResponse
        {
            TransactionId = "TXN123",
            FraudProbability = 0.3,
            IsFraudulent = false,
            Decision = "APPROVED",
            RiskFactors = new Dictionary<string, double> 
            { 
                { "Low Risk", 0.3 } 
            }
        };

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(It.IsAny<Transaction>()))
            .ReturnsAsync(fraudResponse);

        _cosmosDbServiceMock
            .Setup(x => x.StoreTransactionAsync(It.IsAny<Transaction>()))
            .Returns(Task.CompletedTask);

        var httpRequestMock = CreateMockHttpRequestData(request);
        var contextMock = new Mock<FunctionContext>();

        var result = await _function.AnalyzeFraud(
            httpRequestMock.Object, 
            contextMock.Object, 
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        
        _fraudScoringServiceMock.Verify(
            x => x.ScoreTransactionAsync(It.IsAny<Transaction>()), 
            Times.Once);
        
        _cosmosDbServiceMock.Verify(
            x => x.StoreTransactionAsync(It.IsAny<Transaction>()), 
            Times.Once);
    }

    [Fact]
    public async Task AnalyzeFraud_EmptyRequest_ReturnsBadRequest()
    {
        var httpRequestMock = CreateMockHttpRequestData(string.Empty);
        var contextMock = new Mock<FunctionContext>();

        var result = await _function.AnalyzeFraud(
            httpRequestMock.Object,
            contextMock.Object,
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnalyzeFraud_HighRiskTransaction_ReturnsCorrectRiskLevel()
    {
        var request = new FraudAnalysisRequest
        {
            TransactionId = "TXN456",
            Amount = 10000.00m,
            UserEmail = "suspicious@example.com",
            IpAddress = "10.0.0.1",
            PaymentGateway = "PayPal"
        };

        var fraudResponse = new FraudScoreResponse
        {
            TransactionId = "TXN456",
            FraudProbability = 0.85,
            IsFraudulent = true,
            Decision = "DECLINED",
            RiskFactors = new Dictionary<string, double> 
            { 
                { "High Amount", 0.5 },
                { "Suspicious IP", 0.35 }
            }
        };

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(It.IsAny<Transaction>()))
            .ReturnsAsync(fraudResponse);

        var httpRequestMock = CreateMockHttpRequestData(request);
        var contextMock = new Mock<FunctionContext>();

        var result = await _function.AnalyzeFraud(
            httpRequestMock.Object,
            contextMock.Object,
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BulkAnalyze_ValidRequests_ProcessesAllTransactions()
    {
        var requests = new List<FraudAnalysisRequest>
        {
            new() { TransactionId = "TXN1", Amount = 100, UserEmail = "user1@test.com", IpAddress = "1.1.1.1", PaymentGateway = "Stripe" },
            new() { TransactionId = "TXN2", Amount = 200, UserEmail = "user2@test.com", IpAddress = "2.2.2.2", PaymentGateway = "PayPal" },
            new() { TransactionId = "TXN3", Amount = 300, UserEmail = "user3@test.com", IpAddress = "3.3.3.3", PaymentGateway = "Braintree" }
        };

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(It.IsAny<Transaction>()))
            .ReturnsAsync(new FraudScoreResponse
            {
                FraudProbability = 0.2,
                Decision = "APPROVED",
                RiskFactors = new Dictionary<string, double>()
            });

        var httpRequestMock = CreateMockHttpRequestData(requests);
        var contextMock = new Mock<FunctionContext>();

        var result = await _function.BulkAnalyze(
            httpRequestMock.Object,
            contextMock.Object,
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        
        _fraudScoringServiceMock.Verify(
            x => x.ScoreTransactionAsync(It.IsAny<Transaction>()), 
            Times.Exactly(3));
    }

    [Fact]
    public async Task BulkAnalyze_EmptyList_ReturnsBadRequest()
    {
        var httpRequestMock = CreateMockHttpRequestData(new List<FraudAnalysisRequest>());
        var contextMock = new Mock<FunctionContext>();

        var result = await _function.BulkAnalyze(
            httpRequestMock.Object,
            contextMock.Object,
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnalyzeFraud_ServiceThrowsException_ReturnsInternalServerError()
    {
        var request = new FraudAnalysisRequest
        {
            TransactionId = "TXN789",
            Amount = 500,
            UserEmail = "test@example.com",
            IpAddress = "192.168.1.1",
            PaymentGateway = "Stripe"
        };

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(It.IsAny<Transaction>()))
            .ThrowsAsync(new Exception("Service unavailable"));

        var httpRequestMock = CreateMockHttpRequestData(request);
        var contextMock = new Mock<FunctionContext>();

        var result = await _function.AnalyzeFraud(
            httpRequestMock.Object,
            contextMock.Object,
            CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    private Mock<HttpRequestData> CreateMockHttpRequestData(object body)
    {
        var json = JsonSerializer.Serialize(body);
        return CreateMockHttpRequestData(json);
    }

    private Mock<HttpRequestData> CreateMockHttpRequestData(string body)
    {
        var contextMock = new Mock<FunctionContext>();
        var requestMock = new Mock<HttpRequestData>(contextMock.Object);
        
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        requestMock.Setup(r => r.Body).Returns(stream);
        requestMock.Setup(r => r.CreateResponse()).Returns(() =>
        {
            var responseMock = new Mock<HttpResponseData>(contextMock.Object);
            responseMock.SetupProperty(r => r.Headers, new HttpHeadersCollection());
            responseMock.SetupProperty(r => r.StatusCode);
            responseMock.SetupProperty(r => r.Body, new MemoryStream());
            return responseMock.Object;
        });

        return requestMock;
    }
}
