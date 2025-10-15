using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;
using GlobalPaymentFraudDetection.Functions.Triggers;
using GlobalPaymentFraudDetection.Functions.Models;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Functions.Tests.ServiceBusTriggers;

public class AlertProcessingServiceBusTriggerTests
{
    private readonly Mock<ILogger<AlertProcessingServiceBusTrigger>> _loggerMock;
    private readonly Mock<IFraudAlertRepository> _alertRepositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IFraudScoringService> _fraudScoringServiceMock;
    private readonly Mock<ICosmosDbService> _cosmosDbServiceMock;
    private readonly AlertProcessingServiceBusTrigger _function;

    public AlertProcessingServiceBusTriggerTests()
    {
        _loggerMock = new Mock<ILogger<AlertProcessingServiceBusTrigger>>();
        _alertRepositoryMock = new Mock<IFraudAlertRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _fraudScoringServiceMock = new Mock<IFraudScoringService>();
        _cosmosDbServiceMock = new Mock<ICosmosDbService>();

        _function = new AlertProcessingServiceBusTrigger(
            _loggerMock.Object,
            _alertRepositoryMock.Object,
            _notificationServiceMock.Object,
            _fraudScoringServiceMock.Object,
            _cosmosDbServiceMock.Object);
    }

    [Fact]
    public async Task ProcessAlert_CriticalAlert_SendsImmediateNotification()
    {
        var alertMessage = new AlertMessage
        {
            AlertId = "ALERT001",
            TransactionId = "TXN123",
            AlertType = "HIGH_FRAUD_SCORE",
            Severity = "Critical",
            Message = "Critical fraud detected",
            Reasons = new List<string> { "Fraud score > 0.9" }
        };

        var alertJson = JsonSerializer.Serialize(alertMessage);

        _alertRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<FraudAlert>()))
            .Returns(Task.CompletedTask);

        _notificationServiceMock
            .Setup(x => x.SendSmsAlertAsync(It.IsAny<string>(), It.IsAny<FraudAlert>()))
            .Returns(Task.CompletedTask);

        var contextMock = new Mock<FunctionContext>();

        await _function.ProcessAlert(alertJson, contextMock.Object, CancellationToken.None);

        _alertRepositoryMock.Verify(
            x => x.AddAsync(It.Is<FraudAlert>(a => a.Severity == "Critical")),
            Times.Once);

        _notificationServiceMock.Verify(
            x => x.SendSmsAlertAsync(It.IsAny<string>(), It.IsAny<FraudAlert>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAlert_HighSeverityAlert_ProcessesCorrectly()
    {
        var alertMessage = new AlertMessage
        {
            AlertId = "ALERT002",
            TransactionId = "TXN456",
            AlertType = "SUSPICIOUS_VELOCITY",
            Severity = "High",
            Message = "Multiple transactions in short time",
            Reasons = new List<string> { "5 transactions in 10 minutes" }
        };

        var alertJson = JsonSerializer.Serialize(alertMessage);

        _alertRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<FraudAlert>()))
            .Returns(Task.CompletedTask);

        _notificationServiceMock
            .Setup(x => x.SendSmsAlertAsync(It.IsAny<string>(), It.IsAny<FraudAlert>()))
            .Returns(Task.CompletedTask);

        var contextMock = new Mock<FunctionContext>();

        await _function.ProcessAlert(alertJson, contextMock.Object, CancellationToken.None);

        _alertRepositoryMock.Verify(
            x => x.AddAsync(It.Is<FraudAlert>(a => a.Severity == "High")),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAlert_MediumSeverityAlert_QueuesForReview()
    {
        var alertMessage = new AlertMessage
        {
            AlertId = "ALERT003",
            TransactionId = "TXN789",
            AlertType = "UNUSUAL_AMOUNT",
            Severity = "Medium",
            Message = "Transaction amount unusual for user",
            Reasons = new List<string> { "Amount 3x user average" }
        };

        var alertJson = JsonSerializer.Serialize(alertMessage);

        _alertRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<FraudAlert>()))
            .Returns(Task.CompletedTask);

        var contextMock = new Mock<FunctionContext>();

        await _function.ProcessAlert(alertJson, contextMock.Object, CancellationToken.None);

        _alertRepositoryMock.Verify(
            x => x.AddAsync(It.Is<FraudAlert>(a => a.Severity == "Medium")),
            Times.Once);

        _notificationServiceMock.Verify(
            x => x.SendSmsAlertAsync(It.IsAny<string>(), It.IsAny<FraudAlert>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAlert_InvalidJson_HandlesGracefully()
    {
        var invalidJson = "{ invalid json }";
        var contextMock = new Mock<FunctionContext>();

        var act = async () => await _function.ProcessAlert(
            invalidJson,
            contextMock.Object,
            CancellationToken.None);

        await act.Should().NotThrowAsync<JsonException>();
    }

    [Fact]
    public async Task ProcessBatchTransactions_ValidBatch_ProcessesAllTransactions()
    {
        var transactions = new List<FraudAnalysisRequest>
        {
            new() { TransactionId = "T1", Amount = 100, UserEmail = "user1@test.com", IpAddress = "1.1.1.1", PaymentGateway = "Stripe" },
            new() { TransactionId = "T2", Amount = 500, UserEmail = "user2@test.com", IpAddress = "2.2.2.2", PaymentGateway = "PayPal" },
            new() { TransactionId = "T3", Amount = 200, UserEmail = "user3@test.com", IpAddress = "3.3.3.3", PaymentGateway = "Braintree" }
        };

        var batchJson = JsonSerializer.Serialize(transactions);

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(It.IsAny<Transaction>()))
            .ReturnsAsync(new FraudScoreResponse
            {
                FraudProbability = 0.3,
                IsFraudulent = false,
                Decision = "APPROVED",
                RiskFactors = new Dictionary<string, double>()
            });

        _cosmosDbServiceMock
            .Setup(x => x.StoreTransactionAsync(It.IsAny<Transaction>()))
            .Returns(Task.CompletedTask);

        var contextMock = new Mock<FunctionContext>();

        await _function.ProcessBatch(batchJson, contextMock.Object, CancellationToken.None);

        _fraudScoringServiceMock.Verify(
            x => x.ScoreTransactionAsync(It.IsAny<Transaction>()),
            Times.Exactly(3));

        _cosmosDbServiceMock.Verify(
            x => x.StoreTransactionAsync(It.IsAny<Transaction>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessBatchTransactions_HighRiskPercentage_LogsWarning()
    {
        var transactions = new List<FraudAnalysisRequest>
        {
            new() { TransactionId = "T1", Amount = 5000, UserEmail = "user1@test.com", IpAddress = "1.1.1.1", PaymentGateway = "Stripe" },
            new() { TransactionId = "T2", Amount = 6000, UserEmail = "user2@test.com", IpAddress = "2.2.2.2", PaymentGateway = "PayPal" }
        };

        var batchJson = JsonSerializer.Serialize(transactions);

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(It.IsAny<Transaction>()))
            .ReturnsAsync(new FraudScoreResponse
            {
                FraudProbability = 0.8,
                IsFraudulent = true,
                Decision = "DECLINED",
                RiskFactors = new Dictionary<string, double> { { "High Amount", 0.8 } }
            });

        _cosmosDbServiceMock
            .Setup(x => x.StoreTransactionAsync(It.IsAny<Transaction>()))
            .Returns(Task.CompletedTask);

        var contextMock = new Mock<FunctionContext>();

        await _function.ProcessBatch(batchJson, contextMock.Object, CancellationToken.None);

        _fraudScoringServiceMock.Verify(
            x => x.ScoreTransactionAsync(It.IsAny<Transaction>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessBatchTransactions_EmptyBatch_HandlesGracefully()
    {
        var emptyBatch = JsonSerializer.Serialize(new List<FraudAnalysisRequest>());
        var contextMock = new Mock<FunctionContext>();

        var act = async () => await _function.ProcessBatch(
            emptyBatch,
            contextMock.Object,
            CancellationToken.None);

        await act.Should().NotThrowAsync();

        _fraudScoringServiceMock.Verify(
            x => x.ScoreTransactionAsync(It.IsAny<Transaction>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAlert_RepositoryThrowsException_PropagatesException()
    {
        var alertMessage = new AlertMessage
        {
            AlertId = "ALERT999",
            TransactionId = "TXN999",
            AlertType = "TEST",
            Severity = "Low",
            Message = "Test alert"
        };

        var alertJson = JsonSerializer.Serialize(alertMessage);

        _alertRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<FraudAlert>()))
            .ThrowsAsync(new Exception("Database error"));

        var contextMock = new Mock<FunctionContext>();

        var act = async () => await _function.ProcessAlert(
            alertJson,
            contextMock.Object,
            CancellationToken.None);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Database error");
    }
}
