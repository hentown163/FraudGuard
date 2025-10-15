using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Controllers;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Tests.Controllers;

public class FraudDetectionControllerTests
{
    private readonly Mock<IFraudScoringService> _fraudScoringServiceMock;
    private readonly Mock<ILogger<FraudDetectionController>> _loggerMock;
    private readonly FraudDetectionController _controller;

    public FraudDetectionControllerTests()
    {
        _fraudScoringServiceMock = new Mock<IFraudScoringService>();
        _loggerMock = new Mock<ILogger<FraudDetectionController>>();
        _controller = new FraudDetectionController(_fraudScoringServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ScoreTransaction_WithValidTransaction_ReturnsOkResult()
    {
        var transaction = new Transaction
        {
            TransactionId = "TXN123",
            UserId = "USER123",
            Amount = 100.00m
        };

        var fraudResponse = new FraudScoreResponse
        {
            TransactionId = "TXN123",
            FraudProbability = 0.3,
            IsFraudulent = false,
            Decision = "APPROVED"
        };

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ReturnsAsync(fraudResponse);

        var result = await _controller.ScoreTransaction(transaction);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(fraudResponse);
    }

    [Fact]
    public async Task ScoreTransaction_WhenServiceThrows_ReturnsInternalServerError()
    {
        var transaction = new Transaction
        {
            TransactionId = "TXN123",
            UserId = "USER123",
            Amount = 100.00m
        };

        _fraudScoringServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ThrowsAsync(new Exception("Service error"));

        var result = await _controller.ScoreTransaction(transaction);

        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public void Health_ReturnsHealthyStatus()
    {
        var result = _controller.Health();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value as dynamic;
        value.Should().NotBeNull();
    }
}
