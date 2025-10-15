using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using GlobalPaymentFraudDetection.Core.Services;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<IKeyVaultService> _keyVaultServiceMock;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _keyVaultServiceMock = new Mock<IKeyVaultService>();
        _loggerMock = new Mock<ILogger<NotificationService>>();
        _configurationMock = new Mock<IConfiguration>();

        _keyVaultServiceMock.Setup(x => x.GetSecretAsync("TwilioAccountSid")).ReturnsAsync("test-sid");
        _keyVaultServiceMock.Setup(x => x.GetSecretAsync("TwilioAuthToken")).ReturnsAsync("test-token");
        _keyVaultServiceMock.Setup(x => x.GetSecretAsync("TwilioPhoneNumber")).ReturnsAsync("+1234567890");

        _service = new NotificationService(
            _keyVaultServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task SendSmsAlertAsync_WithValidAlert_CompletesSuccessfully()
    {
        var phoneNumber = "+1234567890";
        var alert = new FraudAlert
        {
            AlertId = "ALERT123",
            TransactionId = "TXN123",
            AlertType = "HIGH_FRAUD_SCORE",
            Severity = "Critical"
        };

        await _service.SendSmsAlertAsync(phoneNumber, alert);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SMS alert sent")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailAlertAsync_WithValidAlert_CompletesSuccessfully()
    {
        var email = "test@example.com";
        var alert = new FraudAlert
        {
            AlertId = "ALERT123",
            TransactionId = "TXN123",
            AlertType = "HIGH_FRAUD_SCORE",
            Severity = "Critical"
        };

        await _service.SendEmailAlertAsync(email, alert);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("email alert") || v.ToString()!.Contains("Email alert")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
