using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using GlobalPaymentFraudDetection.Core.Services;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Tests.Services;

public class BehavioralAnalysisServiceTests
{
    private readonly Mock<ICosmosDbService> _cosmosDbServiceMock;
    private readonly Mock<ILogger<BehavioralAnalysisService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly BehavioralAnalysisService _service;

    public BehavioralAnalysisServiceTests()
    {
        _cosmosDbServiceMock = new Mock<ICosmosDbService>();
        _loggerMock = new Mock<ILogger<BehavioralAnalysisService>>();
        _configurationMock = new Mock<IConfiguration>();

        _configurationMock.Setup(x => x["GeoIP:DatabasePath"]).Returns((string?)null);

        _service = new BehavioralAnalysisService(
            _cosmosDbServiceMock.Object,
            _loggerMock.Object,
            _configurationMock.Object
        );
    }

    [Fact]
    public async Task AnalyzeTransactionBehaviorAsync_WithNullUserProfile_ReturnsBasicBehavioralData()
    {
        var transaction = CreateTestTransaction();

        _cosmosDbServiceMock
            .Setup(x => x.GetUserTransactionsAsync(transaction.UserId, It.IsAny<int>()))
            .ReturnsAsync(new List<Transaction>());

        var result = await _service.AnalyzeTransactionBehaviorAsync(transaction, null);

        result.Should().NotBeNull();
        result.UserId.Should().Be(transaction.UserId);
        result.IpAddress.Should().Be(transaction.IpAddress);
        result.AnomalyFlags.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeTransactionBehaviorAsync_WithHighVelocity1Hour_SetsHighVelocityFlag()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();

        var recentTransactions = Enumerable.Range(0, 11)
            .Select(i => new Transaction
            {
                TransactionId = $"TXN{i}",
                UserId = transaction.UserId,
                Timestamp = DateTime.UtcNow.AddMinutes(-i * 5)
            })
            .ToList();

        _cosmosDbServiceMock
            .Setup(x => x.GetUserTransactionsAsync(transaction.UserId, It.IsAny<int>()))
            .ReturnsAsync(recentTransactions);

        var result = await _service.AnalyzeTransactionBehaviorAsync(transaction, userProfile);

        result.AnomalyFlags.Should().Contain("HIGH_VELOCITY_1H");
    }

    [Fact]
    public async Task AnalyzeTransactionBehaviorAsync_WithMultipleDevices_SetsMultipleDevicesFlag()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();

        var recentTransactions = Enumerable.Range(0, 6)
            .Select(i => new Transaction
            {
                TransactionId = $"TXN{i}",
                UserId = transaction.UserId,
                DeviceId = $"DEVICE{i}",
                Timestamp = DateTime.UtcNow.AddHours(-i)
            })
            .ToList();

        _cosmosDbServiceMock
            .Setup(x => x.GetUserTransactionsAsync(transaction.UserId, It.IsAny<int>()))
            .ReturnsAsync(recentTransactions);

        var result = await _service.AnalyzeTransactionBehaviorAsync(transaction, userProfile);

        result.AnomalyFlags.Should().Contain("MULTIPLE_DEVICES");
        result.Velocity.Should().NotBeNull();
        result.Velocity!.UniqueDevicesLast24Hours.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task AnalyzeTransactionBehaviorAsync_WithUnusualAmount_SetsUnusualAmountFlag()
    {
        var transaction = CreateTestTransaction();
        transaction.Amount = 1500m;

        var userProfile = CreateTestUserProfile();
        userProfile.AvgAmount = 100m;

        _cosmosDbServiceMock
            .Setup(x => x.GetUserTransactionsAsync(transaction.UserId, It.IsAny<int>()))
            .ReturnsAsync(new List<Transaction>());

        var result = await _service.AnalyzeTransactionBehaviorAsync(transaction, userProfile);

        result.AnomalyFlags.Should().Contain("UNUSUAL_AMOUNT");
    }

    [Fact]
    public async Task CalculateVelocityAsync_CountsTransactionsCorrectly()
    {
        var userId = "USER123";
        var now = DateTime.UtcNow;

        var transactions = new List<Transaction>
        {
            new() { TransactionId = "T1", UserId = userId, Timestamp = now.AddMinutes(-10), DeviceId = "D1", IpAddress = "IP1", Amount = 100 },
            new() { TransactionId = "T2", UserId = userId, Timestamp = now.AddMinutes(-30), DeviceId = "D1", IpAddress = "IP1", Amount = 150 },
            new() { TransactionId = "T3", UserId = userId, Timestamp = now.AddHours(-2), DeviceId = "D2", IpAddress = "IP2", Amount = 200 },
            new() { TransactionId = "T4", UserId = userId, Timestamp = now.AddHours(-10), DeviceId = "D3", IpAddress = "IP3", Amount = 250 },
            new() { TransactionId = "T5", UserId = userId, Timestamp = now.AddDays(-5), DeviceId = "D1", IpAddress = "IP1", Amount = 300 }
        };

        _cosmosDbServiceMock
            .Setup(x => x.GetUserTransactionsAsync(userId, It.IsAny<int>()))
            .ReturnsAsync(transactions);

        var result = await _service.CalculateVelocityAsync(userId);

        result.TransactionsLast1Hour.Should().Be(2);
        result.TransactionsLast24Hours.Should().Be(4);
        result.AmountLast1Hour.Should().Be(250m);
        result.UniqueDevicesLast24Hours.Should().Be(3);
        result.UniqueIpsLast24Hours.Should().Be(3);
    }

    [Fact]
    public async Task GetGeoLocationAsync_WithoutGeoIpReader_ReturnsEmptyGeoLocationData()
    {
        var ipAddress = "192.168.1.1";

        var result = await _service.GetGeoLocationAsync(ipAddress);

        result.Should().NotBeNull();
        result!.Country.Should().BeEmpty();
        result.City.Should().BeEmpty();
    }

    private Transaction CreateTestTransaction()
    {
        return new Transaction
        {
            TransactionId = "TXN123",
            UserId = "USER123",
            Amount = 100.00m,
            Currency = "USD",
            IpAddress = "192.168.1.1",
            DeviceId = "DEVICE123",
            PaymentGateway = "Stripe",
            Timestamp = DateTime.UtcNow
        };
    }

    private UserProfile CreateTestUserProfile()
    {
        return new UserProfile
        {
            UserId = "USER123",
            TotalTransactions = 50,
            TotalSpent = 5000m,
            AvgAmount = 100m,
            FirstTransactionDate = DateTime.UtcNow.AddMonths(-6),
            LastTransactionDate = DateTime.UtcNow.AddDays(-1)
        };
    }
}
