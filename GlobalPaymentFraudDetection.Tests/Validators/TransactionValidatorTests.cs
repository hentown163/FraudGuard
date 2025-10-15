using Xunit;
using FluentAssertions;
using GlobalPaymentFraudDetection.Validators;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Tests.Validators;

public class TransactionValidatorTests
{
    private readonly TransactionValidator _validator;

    public TransactionValidatorTests()
    {
        _validator = new TransactionValidator();
    }

    [Fact]
    public void Validate_WithValidTransaction_PassesValidation()
    {
        var transaction = new Transaction
        {
            TransactionId = "TXN123",
            UserId = "USER123",
            Amount = 100.00m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow,
            IpAddress = "192.168.1.1",
            PaymentGateway = "Stripe",
            PaymentMethod = "Credit Card",
            Country = "US"
        };

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyTransactionId_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.TransactionId = "";

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TransactionId");
    }

    [Fact]
    public void Validate_WithNegativeAmount_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.Amount = -10m;

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void Validate_WithZeroAmount_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.Amount = 0m;

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void Validate_WithAmountExceedingLimit_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.Amount = 2000000m;

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Amount");
    }

    [Fact]
    public void Validate_WithInvalidCurrency_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.Currency = "XXX";

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Currency");
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    public void Validate_WithValidCurrency_PassesValidation(string currency)
    {
        var transaction = CreateValidTransaction();
        transaction.Currency = currency;

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOldTimestamp_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.Timestamp = DateTime.UtcNow.AddMinutes(-10);

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Timestamp");
    }

    [Fact]
    public void Validate_WithFutureTimestamp_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.Timestamp = DateTime.UtcNow.AddMinutes(5);

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Timestamp");
    }

    [Fact]
    public void Validate_WithInvalidIpAddress_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.IpAddress = "999.999.999.999";

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "IpAddress");
    }

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    public void Validate_WithValidIpAddress_PassesValidation(string ipAddress)
    {
        var transaction = CreateValidTransaction();
        transaction.IpAddress = ipAddress;

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Stripe")]
    [InlineData("PayPal")]
    [InlineData("Braintree")]
    [InlineData("Authorize.Net")]
    public void Validate_WithValidPaymentGateway_PassesValidation(string gateway)
    {
        var transaction = CreateValidTransaction();
        transaction.PaymentGateway = gateway;

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidPaymentGateway_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.PaymentGateway = "InvalidGateway";

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PaymentGateway");
    }

    [Fact]
    public void Validate_WithInvalidCountryCode_FailsValidation()
    {
        var transaction = CreateValidTransaction();
        transaction.Country = "USA";

        var result = _validator.Validate(transaction);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Country");
    }

    private Transaction CreateValidTransaction()
    {
        return new Transaction
        {
            TransactionId = "TXN123",
            UserId = "USER123",
            Amount = 100.00m,
            Currency = "USD",
            Timestamp = DateTime.UtcNow,
            IpAddress = "192.168.1.1",
            PaymentGateway = "Stripe",
            PaymentMethod = "Credit Card",
            Country = "US"
        };
    }
}
