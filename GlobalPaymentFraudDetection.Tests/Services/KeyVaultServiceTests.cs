using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using GlobalPaymentFraudDetection.Core.Services;

namespace GlobalPaymentFraudDetection.Tests.Services;

public class KeyVaultServiceTests
{
    [Fact]
    public void Constructor_WithoutVaultUri_ThrowsArgumentNullException()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x["KeyVault:VaultUri"]).Returns((string?)null);

        Assert.Throws<ArgumentNullException>(() => new KeyVaultService(configMock.Object));
    }

    [Fact]
    public void Constructor_WithValidVaultUri_CreatesInstance()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x["KeyVault:VaultUri"]).Returns("https://test-vault.vault.azure.net/");

        var service = new KeyVaultService(configMock.Object);

        service.Should().NotBeNull();
    }
}
