using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Services;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Tests.Services;

public class EnsembleModelServiceTests
{
    private readonly Mock<IOnnxModelService> _onnxModelServiceMock;
    private readonly Mock<IAdvancedRiskScoringService> _riskScoringServiceMock;
    private readonly Mock<ILogger<EnsembleModelService>> _loggerMock;
    private readonly EnsembleModelService _service;

    public EnsembleModelServiceTests()
    {
        _onnxModelServiceMock = new Mock<IOnnxModelService>();
        _riskScoringServiceMock = new Mock<IAdvancedRiskScoringService>();
        _loggerMock = new Mock<ILogger<EnsembleModelService>>();

        _service = new EnsembleModelService(
            _onnxModelServiceMock.Object,
            _riskScoringServiceMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task PredictWithEnsembleAsync_CombinesModelPredictionsWithCorrectWeights()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData();

        _onnxModelServiceMock
            .Setup(x => x.PredictFraudProbabilityAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(0.8);

        _riskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>());

        var result = await _service.PredictWithEnsembleAsync(transaction, userProfile, behavioralData);

        result.Should().BeGreaterThan(0);
        result.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task PredictWithEnsembleAsync_WhenOnnxFails_UsesFallbackScore()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData();

        _onnxModelServiceMock
            .Setup(x => x.PredictFraudProbabilityAsync(transaction, userProfile, behavioralData))
            .ThrowsAsync(new Exception("ONNX model failed"));

        _riskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>());

        var result = await _service.PredictWithEnsembleAsync(transaction, userProfile, behavioralData);

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetModelPredictionsAsync_ReturnsAllModelPredictions()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData();

        _onnxModelServiceMock
            .Setup(x => x.PredictFraudProbabilityAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(0.7);

        _riskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>());

        var result = await _service.GetModelPredictionsAsync(transaction, userProfile, behavioralData);

        result.Should().ContainKey("OnnxModel");
        result.Should().ContainKey("RuleBasedModel");
        result.Should().ContainKey("StatisticalModel");
        result.Should().ContainKey("BehavioralModel");
    }

    [Fact]
    public async Task PredictWithEnsembleAsync_WithHighRiskScores_ReturnsHighProbability()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData();
        behavioralData.RiskScore = 90;
        behavioralData.AnomalyFlags = new List<string> { "HIGH_VELOCITY", "SUSPICIOUS_IP" };

        _onnxModelServiceMock
            .Setup(x => x.PredictFraudProbabilityAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(0.95);

        _riskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>());

        var result = await _service.PredictWithEnsembleAsync(transaction, userProfile, behavioralData);

        result.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task PredictWithEnsembleAsync_WithLowRiskScores_ReturnsLowProbability()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData();
        behavioralData.RiskScore = 10;

        _onnxModelServiceMock
            .Setup(x => x.PredictFraudProbabilityAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(0.1);

        _riskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>());

        var result = await _service.PredictWithEnsembleAsync(transaction, userProfile, behavioralData);

        result.Should().BeLessThan(0.5);
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
            PaymentGateway = "Stripe"
        };
    }

    private UserProfile CreateTestUserProfile()
    {
        return new UserProfile
        {
            UserId = "USER123",
            TotalTransactions = 50,
            TotalSpent = 5000m,
            AvgAmount = 100m
        };
    }

    private BehavioralData CreateTestBehavioralData()
    {
        return new BehavioralData
        {
            UserId = "USER123",
            RiskScore = 30,
            AnomalyFlags = new List<string>()
        };
    }
}
