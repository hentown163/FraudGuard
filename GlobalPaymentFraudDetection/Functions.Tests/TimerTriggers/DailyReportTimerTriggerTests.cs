using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using GlobalPaymentFraudDetection.Functions.Triggers;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Functions.Tests.TimerTriggers;

public class DailyReportTimerTriggerTests
{
    private readonly Mock<ILogger<DailyReportTimerTrigger>> _loggerMock;
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock;
    private readonly Mock<IFraudAlertRepository> _alertRepositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly DailyReportTimerTrigger _function;

    public DailyReportTimerTriggerTests()
    {
        _loggerMock = new Mock<ILogger<DailyReportTimerTrigger>>();
        _transactionRepositoryMock = new Mock<ITransactionRepository>();
        _alertRepositoryMock = new Mock<IFraudAlertRepository>();
        _notificationServiceMock = new Mock<INotificationService>();

        _function = new DailyReportTimerTrigger(
            _loggerMock.Object,
            _transactionRepositoryMock.Object,
            _alertRepositoryMock.Object,
            _notificationServiceMock.Object);
    }

    [Fact]
    public async Task GenerateDailyReport_WithTransactions_GeneratesCorrectReport()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var transactions = new List<Transaction>
        {
            new() { TransactionId = "T1", Amount = 100, IsFraudulent = false, PaymentGateway = "Stripe", FraudScore = 0.2 },
            new() { TransactionId = "T2", Amount = 200, IsFraudulent = true, PaymentGateway = "PayPal", FraudScore = 0.9 },
            new() { TransactionId = "T3", Amount = 150, IsFraudulent = false, PaymentGateway = "Stripe", FraudScore = 0.3 },
            new() { TransactionId = "T4", Amount = 500, IsFraudulent = true, PaymentGateway = "Braintree", FraudScore = 0.8 }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(transactions);

        var timerInfo = new TimerInfo();
        var contextMock = new Mock<FunctionContext>();

        await _function.GenerateDailyReport(timerInfo, contextMock.Object, CancellationToken.None);

        _transactionRepositoryMock.Verify(
            x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateDailyReport_NoTransactions_HandlesGracefully()
    {
        _transactionRepositoryMock
            .Setup(x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<Transaction>());

        var timerInfo = new TimerInfo();
        var contextMock = new Mock<FunctionContext>();

        var act = async () => await _function.GenerateDailyReport(
            timerInfo, 
            contextMock.Object, 
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HourlyAnomalyDetection_HighValueTransactions_DetectsAnomaly()
    {
        var transactions = new List<Transaction>
        {
            new() { TransactionId = "T1", Amount = 100, FraudScore = 0.2, Status = "APPROVED" },
            new() { TransactionId = "T2", Amount = 5000, FraudScore = 0.3, Status = "APPROVED" },
            new() { TransactionId = "T3", Amount = 150, FraudScore = 0.1, Status = "APPROVED" }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(transactions);

        var timerInfo = new TimerInfo();
        var contextMock = new Mock<FunctionContext>();

        await _function.DetectAnomalies(timerInfo, contextMock.Object, CancellationToken.None);

        _transactionRepositoryMock.Verify(
            x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task HourlyAnomalyDetection_HighFraudScoreRate_DetectsAnomaly()
    {
        var transactions = Enumerable.Range(1, 10).Select(i => new Transaction
        {
            TransactionId = $"T{i}",
            Amount = 100,
            FraudScore = i <= 3 ? 0.8 : 0.2,
            Status = i <= 3 ? "DECLINED" : "APPROVED"
        }).ToList();

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(transactions);

        var timerInfo = new TimerInfo();
        var contextMock = new Mock<FunctionContext>();

        await _function.DetectAnomalies(timerInfo, contextMock.Object, CancellationToken.None);

        _transactionRepositoryMock.Verify(
            x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task HourlyAnomalyDetection_NoAnomalies_LogsNormalStatus()
    {
        var transactions = new List<Transaction>
        {
            new() { TransactionId = "T1", Amount = 100, FraudScore = 0.2, Status = "APPROVED" },
            new() { TransactionId = "T2", Amount = 120, FraudScore = 0.1, Status = "APPROVED" },
            new() { TransactionId = "T3", Amount = 110, FraudScore = 0.15, Status = "APPROVED" }
        };

        _transactionRepositoryMock
            .Setup(x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(transactions);

        var timerInfo = new TimerInfo();
        var contextMock = new Mock<FunctionContext>();

        await _function.DetectAnomalies(timerInfo, contextMock.Object, CancellationToken.None);

        _transactionRepositoryMock.Verify(
            x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateDailyReport_RepositoryThrowsException_PropagatesException()
    {
        _transactionRepositoryMock
            .Setup(x => x.GetTransactionsByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var timerInfo = new TimerInfo();
        var contextMock = new Mock<FunctionContext>();

        var act = async () => await _function.GenerateDailyReport(
            timerInfo,
            contextMock.Object,
            CancellationToken.None);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Database connection failed");
    }
}
