using GlobalPaymentFraudDetection.Models;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;

namespace GlobalPaymentFraudDetection.Tests.Utilities;

public static class TestHelpers
{
    public static Transaction CreateTestTransaction(
        string transactionId = "TXN123",
        string userId = "USER123",
        decimal amount = 100.00m)
    {
        return new Transaction
        {
            TransactionId = transactionId,
            UserId = userId,
            Amount = amount,
            Currency = "USD",
            Timestamp = DateTime.UtcNow,
            IpAddress = "192.168.1.1",
            DeviceId = "DEVICE123",
            PaymentGateway = "Stripe",
            PaymentMethod = "Credit Card",
            Country = "US",
            Status = "PENDING"
        };
    }

    public static UserProfile CreateTestUserProfile(
        string userId = "USER123",
        int totalTransactions = 50,
        decimal avgAmount = 100m)
    {
        return new UserProfile
        {
            UserId = userId,
            TotalTransactions = totalTransactions,
            TotalSpent = avgAmount * totalTransactions,
            AvgAmount = avgAmount,
            FirstTransactionDate = DateTime.UtcNow.AddMonths(-6),
            LastTransactionDate = DateTime.UtcNow.AddDays(-1),
            UsedIpAddresses = new List<string> { "192.168.1.1" },
            UsedDevices = new List<string> { "DEVICE123" }
        };
    }

    public static BehavioralData CreateTestBehavioralData(
        string userId = "USER123",
        double riskScore = 30)
    {
        return new BehavioralData
        {
            UserId = userId,
            RiskScore = riskScore,
            AnomalyFlags = new List<string>(),
            Velocity = new TransactionVelocity
            {
                TransactionsLast1Hour = 2,
                TransactionsLast24Hours = 10,
                TransactionsLast7Days = 30,
                AmountLast1Hour = 200m,
                AmountLast24Hours = 1000m,
                UniqueDevicesLast24Hours = 1,
                UniqueIpsLast24Hours = 1,
                VelocityScore = 0.3
            }
        };
    }

    public static FraudAlert CreateTestFraudAlert(
        string alertId = "ALERT123",
        string transactionId = "TXN123",
        string severity = "High")
    {
        return new FraudAlert
        {
            AlertId = alertId,
            TransactionId = transactionId,
            AlertType = "HIGH_FRAUD_SCORE",
            Severity = severity,
            CreatedAt = DateTime.UtcNow,
            Status = "UNRESOLVED",
            Reasons = new List<string> { "High fraud probability detected" }
        };
    }

    public static FraudScoreResponse CreateTestFraudScoreResponse(
        string transactionId = "TXN123",
        double fraudProbability = 0.3,
        bool isFraudulent = false)
    {
        return new FraudScoreResponse
        {
            TransactionId = transactionId,
            FraudProbability = fraudProbability,
            IsFraudulent = isFraudulent,
            Decision = isFraudulent ? "DECLINED" : "APPROVED",
            Reason = isFraudulent ? "High fraud risk" : "Low fraud risk",
            ProcessedAt = DateTime.UtcNow,
            RiskFactors = new Dictionary<string, double>
            {
                { "EnsembleScore", fraudProbability },
                { "BehavioralRisk", 0.2 },
                { "VelocityRisk", 0.1 }
            },
            ReviewStatus = "AUTO"
        };
    }

    public static SiftScienceResponse CreateTestSiftScienceResponse(
        double score = 0.3,
        string status = "SUCCESS")
    {
        return new SiftScienceResponse
        {
            Score = score,
            Status = status,
            Reasons = new List<string> { "Low risk transaction" }
        };
    }

    public static PaymentGatewayTransaction CreateTestPaymentGatewayTransaction(
        string gatewayTransactionId = "STRIPE123",
        string gateway = "Stripe",
        decimal amount = 100m)
    {
        return new PaymentGatewayTransaction
        {
            GatewayTransactionId = gatewayTransactionId,
            Gateway = gateway,
            Amount = amount,
            Currency = "USD",
            CustomerId = "CUST123",
            Status = "succeeded",
            CreatedAt = DateTime.UtcNow
        };
    }
}
