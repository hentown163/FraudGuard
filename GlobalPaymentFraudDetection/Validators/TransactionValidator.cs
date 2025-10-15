using FluentValidation;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Validators;

public class TransactionValidator : AbstractValidator<Transaction>
{
    public TransactionValidator()
    {
        RuleFor(x => x.TransactionId)
            .NotEmpty().WithMessage("Transaction ID is required")
            .Length(1, 100).WithMessage("Transaction ID must be between 1 and 100 characters");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required")
            .Length(1, 100).WithMessage("User ID must be between 1 and 100 characters");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than 0")
            .LessThanOrEqualTo(1000000).WithMessage("Amount cannot exceed 1,000,000");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be a valid 3-letter ISO code")
            .Must(BeValidCurrency).WithMessage("Currency must be a valid ISO currency code");

        RuleFor(x => x.Timestamp)
            .NotEmpty().WithMessage("Timestamp is required")
            .Must(NotBePastDate).WithMessage("Transaction timestamp cannot be in the past (older than 5 minutes)")
            .Must(NotBeFutureDate).WithMessage("Transaction timestamp cannot be in the future (more than 1 minute ahead)");

        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("IP Address is required")
            .Must(BeValidIpAddress).WithMessage("IP Address must be valid");

        RuleFor(x => x.PaymentGateway)
            .NotEmpty().WithMessage("Payment Gateway is required")
            .Must(BeValidPaymentGateway).WithMessage("Payment Gateway must be valid (Stripe, PayPal, Braintree, or Authorize.Net)");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Payment Method is required");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required")
            .Length(2).WithMessage("Country must be a valid 2-letter ISO code");
    }

    private bool NotBePastDate(DateTime timestamp)
    {
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        return timestamp >= fiveMinutesAgo;
    }

    private bool NotBeFutureDate(DateTime timestamp)
    {
        var oneMinuteAhead = DateTime.UtcNow.AddMinutes(1);
        return timestamp <= oneMinuteAhead;
    }

    private bool BeValidCurrency(string currency)
    {
        var validCurrencies = new[] 
        { 
            "USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF", "CNY", "INR",
            "MXN", "BRL", "ARS", "CLP", "COP", "PEN", "SEK", "NOK", "DKK",
            "PLN", "CZK", "HUF", "RUB", "TRY", "ZAR", "KRW", "SGD", "HKD",
            "NZD", "THB", "MYR", "PHP", "IDR", "VND", "ILS", "SAR", "AED",
            "EGP", "NGN", "KES", "GHS", "PKR", "BDT", "LKR", "NPR"
        };
        return currency.Length == 3 && validCurrencies.Contains(currency.ToUpper());
    }

    private bool BeValidIpAddress(string ipAddress)
    {
        return System.Net.IPAddress.TryParse(ipAddress, out _);
    }

    private bool BeValidPaymentGateway(string gateway)
    {
        var validGateways = new[] { "Stripe", "PayPal", "Braintree", "Authorize.Net", "AuthorizeNet" };
        return validGateways.Contains(gateway, StringComparer.OrdinalIgnoreCase);
    }
}
