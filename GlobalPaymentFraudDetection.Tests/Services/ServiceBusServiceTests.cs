using Xunit;
using Moq;
using FluentAssertions;
using Azure.Messaging.ServiceBus;
using GlobalPaymentFraudDetection.Core.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Tests.Services;

public class ServiceBusServiceTests
{
    [Fact]
    public async Task SendFraudAlertAsync_CreatesCorrectMessage()
    {
        var mockClient = new Mock<ServiceBusClient>();
        var mockSender = new Mock<ServiceBusSender>();

        mockClient
            .Setup(x => x.CreateSender("fraud-alerts"))
            .Returns(mockSender.Object);

        var service = new ServiceBusService(mockClient.Object);

        var alert = new FraudAlert
        {
            AlertId = "ALERT123",
            TransactionId = "TXN123",
            AlertType = "HIGH_FRAUD_SCORE"
        };

        await service.SendFraudAlertAsync(alert);

        mockSender.Verify(
            x => x.SendMessageAsync(
                It.Is<ServiceBusMessage>(m => 
                    m.ContentType == "application/json" && 
                    m.MessageId == alert.AlertId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTransactionEventAsync_CreatesCorrectMessage()
    {
        var mockClient = new Mock<ServiceBusClient>();
        var mockSender = new Mock<ServiceBusSender>();

        mockClient
            .Setup(x => x.CreateSender("transaction-events"))
            .Returns(mockSender.Object);

        var service = new ServiceBusService(mockClient.Object);

        var transaction = new Transaction
        {
            TransactionId = "TXN123",
            UserId = "USER123",
            Amount = 100m
        };

        await service.SendTransactionEventAsync(transaction);

        mockSender.Verify(
            x => x.SendMessageAsync(
                It.Is<ServiceBusMessage>(m => 
                    m.ContentType == "application/json" && 
                    m.MessageId == transaction.TransactionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
