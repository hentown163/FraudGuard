using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using GlobalPaymentFraudDetection.Core.Services;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Infrastructure;
using GlobalPaymentFraudDetection.Models;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;

namespace GlobalPaymentFraudDetection.Tests.Services;

public class FraudScoringServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IEnsembleModelService> _ensembleModelServiceMock;
    private readonly Mock<IServiceBusService> _serviceBusServiceMock;
    private readonly Mock<IBehavioralAnalysisService> _behavioralAnalysisServiceMock;
    private readonly Mock<IAdvancedRiskScoringService> _advancedRiskScoringServiceMock;
    private readonly Mock<IFraudRulesEngine> _fraudRulesEngineMock;
    private readonly Mock<ISiftScienceService> _siftScienceServiceMock;
    private readonly Mock<ILogger<FraudScoringService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IUserProfileRepository> _userProfileRepositoryMock;
    private readonly FraudScoringService _service;

    public FraudScoringServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _ensembleModelServiceMock = new Mock<IEnsembleModelService>();
        _serviceBusServiceMock = new Mock<IServiceBusService>();
        _behavioralAnalysisServiceMock = new Mock<IBehavioralAnalysisService>();
        _advancedRiskScoringServiceMock = new Mock<IAdvancedRiskScoringService>();
        _fraudRulesEngineMock = new Mock<IFraudRulesEngine>();
        _siftScienceServiceMock = new Mock<ISiftScienceService>();
        _loggerMock = new Mock<ILogger<FraudScoringService>>();
        _configurationMock = new Mock<IConfiguration>();
        _userProfileRepositoryMock = new Mock<IUserProfileRepository>();

        _unitOfWorkMock.Setup(x => x.UserProfiles).Returns(_userProfileRepositoryMock.Object);

        _configurationMock.Setup(x => x["FraudDetection:Threshold"]).Returns("0.7");

        _service = new FraudScoringService(
            _unitOfWorkMock.Object,
            _ensembleModelServiceMock.Object,
            _serviceBusServiceMock.Object,
            _behavioralAnalysisServiceMock.Object,
            _advancedRiskScoringServiceMock.Object,
            _fraudRulesEngineMock.Object,
            _siftScienceServiceMock.Object,
            _loggerMock.Object,
            _configurationMock.Object
        );
    }

    [Fact]
    public async Task ScoreTransactionAsync_WhenTransactionIsBlocked_ReturnsBlockedResponse()
    {
        var transaction = CreateTestTransaction();
        
        _fraudRulesEngineMock
            .Setup(x => x.ShouldBlockTransactionAsync(transaction))
            .ReturnsAsync(true);

        var result = await _service.ScoreTransactionAsync(transaction);

        result.Should().NotBeNull();
        result.TransactionId.Should().Be(transaction.TransactionId);
        result.IsFraudulent.Should().BeTrue();
        result.Decision.Should().Be("BLOCKED");
        result.FraudProbability.Should().Be(1.0);
        result.ReviewStatus.Should().Be("BLOCKED");
    }

    [Fact]
    public async Task ScoreTransactionAsync_WithLowFraudProbability_ReturnsApprovedDecision()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData(riskScore: 20);

        _fraudRulesEngineMock
            .Setup(x => x.ShouldBlockTransactionAsync(transaction))
            .ReturnsAsync(false);

        _userProfileRepositoryMock
            .Setup(x => x.GetByUserIdAsync(transaction.UserId))
            .ReturnsAsync(userProfile);

        _behavioralAnalysisServiceMock
            .Setup(x => x.AnalyzeTransactionBehaviorAsync(transaction, userProfile))
            .ReturnsAsync(behavioralData);

        _ensembleModelServiceMock
            .Setup(x => x.PredictWithEnsembleAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(0.3);

        _advancedRiskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>
            {
                { "VelocityRisk", 0.1 },
                { "DeviceRisk", 0.2 },
                { "GeolocationRisk", 0.15 }
            });

        _fraudRulesEngineMock
            .Setup(x => x.EvaluateRulesAsync(transaction, userProfile))
            .ReturnsAsync(new List<string>());

        _fraudRulesEngineMock
            .Setup(x => x.RequiresManualReviewAsync(transaction, It.IsAny<double>()))
            .ReturnsAsync(false);

        _siftScienceServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ReturnsAsync(new SiftScienceResponse { Score = 0.2, Status = "SUCCESS" });

        var result = await _service.ScoreTransactionAsync(transaction);

        result.Should().NotBeNull();
        result.IsFraudulent.Should().BeFalse();
        result.Decision.Should().Be("APPROVED");
        result.FraudProbability.Should().BeLessThan(0.7);
        result.ReviewStatus.Should().Be("AUTO");
    }

    [Fact]
    public async Task ScoreTransactionAsync_WithHighFraudProbability_ReturnsDeclinedDecision()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData(riskScore: 85);

        _fraudRulesEngineMock
            .Setup(x => x.ShouldBlockTransactionAsync(transaction))
            .ReturnsAsync(false);

        _userProfileRepositoryMock
            .Setup(x => x.GetByUserIdAsync(transaction.UserId))
            .ReturnsAsync(userProfile);

        _behavioralAnalysisServiceMock
            .Setup(x => x.AnalyzeTransactionBehaviorAsync(transaction, userProfile))
            .ReturnsAsync(behavioralData);

        _ensembleModelServiceMock
            .Setup(x => x.PredictWithEnsembleAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(0.9);

        _advancedRiskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>
            {
                { "VelocityRisk", 0.8 },
                { "DeviceRisk", 0.7 },
                { "GeolocationRisk", 0.85 }
            });

        _fraudRulesEngineMock
            .Setup(x => x.EvaluateRulesAsync(transaction, userProfile))
            .ReturnsAsync(new List<string> { "High velocity", "Suspicious IP" });

        _fraudRulesEngineMock
            .Setup(x => x.RequiresManualReviewAsync(transaction, It.IsAny<double>()))
            .ReturnsAsync(false);

        _siftScienceServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ReturnsAsync(new SiftScienceResponse { Score = 0.85, Status = "SUCCESS" });

        var result = await _service.ScoreTransactionAsync(transaction);

        result.Should().NotBeNull();
        result.IsFraudulent.Should().BeTrue();
        result.Decision.Should().Be("DECLINED");
        result.FraudProbability.Should().BeGreaterThan(0.7);
        result.RiskFactors.Should().ContainKey("EnsembleScore");
        result.RiskFactors.Should().ContainKey("SiftScienceScore");
    }

    [Fact]
    public async Task ScoreTransactionAsync_RequiresManualReview_SetsCorrectReviewStatus()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData(riskScore: 60);

        _fraudRulesEngineMock
            .Setup(x => x.ShouldBlockTransactionAsync(transaction))
            .ReturnsAsync(false);

        _userProfileRepositoryMock
            .Setup(x => x.GetByUserIdAsync(transaction.UserId))
            .ReturnsAsync(userProfile);

        _behavioralAnalysisServiceMock
            .Setup(x => x.AnalyzeTransactionBehaviorAsync(transaction, userProfile))
            .ReturnsAsync(behavioralData);

        _ensembleModelServiceMock
            .Setup(x => x.PredictWithEnsembleAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(0.65);

        _advancedRiskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>());

        _fraudRulesEngineMock
            .Setup(x => x.EvaluateRulesAsync(transaction, userProfile))
            .ReturnsAsync(new List<string>());

        _fraudRulesEngineMock
            .Setup(x => x.RequiresManualReviewAsync(transaction, It.IsAny<double>()))
            .ReturnsAsync(true);

        _siftScienceServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ReturnsAsync(new SiftScienceResponse { Score = 0.5, Status = "SUCCESS" });

        var result = await _service.ScoreTransactionAsync(transaction);

        result.Should().NotBeNull();
        result.ReviewStatus.Should().Be("MANUAL_REVIEW");
    }

    [Fact]
    public async Task ScoreTransactionAsync_CombinesSiftScienceScore_Correctly()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData(riskScore: 40);

        _fraudRulesEngineMock
            .Setup(x => x.ShouldBlockTransactionAsync(transaction))
            .ReturnsAsync(false);

        _userProfileRepositoryMock
            .Setup(x => x.GetByUserIdAsync(transaction.UserId))
            .ReturnsAsync(userProfile);

        _behavioralAnalysisServiceMock
            .Setup(x => x.AnalyzeTransactionBehaviorAsync(transaction, userProfile))
            .ReturnsAsync(behavioralData);

        var ensembleScore = 0.5;
        _ensembleModelServiceMock
            .Setup(x => x.PredictWithEnsembleAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(ensembleScore);

        _advancedRiskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>());

        _fraudRulesEngineMock
            .Setup(x => x.EvaluateRulesAsync(transaction, userProfile))
            .ReturnsAsync(new List<string>());

        _fraudRulesEngineMock
            .Setup(x => x.RequiresManualReviewAsync(transaction, It.IsAny<double>()))
            .ReturnsAsync(false);

        var siftScore = 0.8;
        _siftScienceServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ReturnsAsync(new SiftScienceResponse { Score = siftScore, Status = "SUCCESS" });

        var result = await _service.ScoreTransactionAsync(transaction);

        var expectedCombinedScore = (ensembleScore * 0.7) + (siftScore * 0.3);
        result.FraudProbability.Should().BeApproximately(expectedCombinedScore, 0.01);
    }

    [Fact]
    public async Task ScoreTransactionAsync_IncludesAllRiskFactors_InResponse()
    {
        var transaction = CreateTestTransaction();
        var userProfile = CreateTestUserProfile();
        var behavioralData = CreateTestBehavioralData(riskScore: 50);

        _fraudRulesEngineMock
            .Setup(x => x.ShouldBlockTransactionAsync(transaction))
            .ReturnsAsync(false);

        _userProfileRepositoryMock
            .Setup(x => x.GetByUserIdAsync(transaction.UserId))
            .ReturnsAsync(userProfile);

        _behavioralAnalysisServiceMock
            .Setup(x => x.AnalyzeTransactionBehaviorAsync(transaction, userProfile))
            .ReturnsAsync(behavioralData);

        _ensembleModelServiceMock
            .Setup(x => x.PredictWithEnsembleAsync(transaction, userProfile, behavioralData))
            .ReturnsAsync(0.6);

        _advancedRiskScoringServiceMock
            .Setup(x => x.CalculateAllRiskScoresAsync(transaction, userProfile))
            .ReturnsAsync(new Dictionary<string, double>
            {
                { "VelocityRisk", 0.5 },
                { "DeviceRisk", 0.4 },
                { "GeolocationRisk", 0.3 },
                { "AmountRisk", 0.35 },
                { "TimeRisk", 0.25 }
            });

        _fraudRulesEngineMock
            .Setup(x => x.EvaluateRulesAsync(transaction, userProfile))
            .ReturnsAsync(new List<string> { "Rule1" });

        _fraudRulesEngineMock
            .Setup(x => x.RequiresManualReviewAsync(transaction, It.IsAny<double>()))
            .ReturnsAsync(false);

        _siftScienceServiceMock
            .Setup(x => x.ScoreTransactionAsync(transaction))
            .ReturnsAsync(new SiftScienceResponse { Score = 0.6, Status = "SUCCESS" });

        var result = await _service.ScoreTransactionAsync(transaction);

        result.RiskFactors.Should().ContainKey("EnsembleScore");
        result.RiskFactors.Should().ContainKey("SiftScienceScore");
        result.RiskFactors.Should().ContainKey("BehavioralRisk");
        result.RiskFactors.Should().ContainKey("VelocityRisk");
        result.RiskFactors.Should().ContainKey("DeviceRisk");
        result.RiskFactors.Should().ContainKey("GeolocationRisk");
        result.RiskFactors.Should().ContainKey("AmountRisk");
        result.RiskFactors.Should().ContainKey("TimeRisk");
        result.RiskFactors.Should().ContainKey("RuleViolations");
        result.RiskFactors["RuleViolations"].Should().Be(1);
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
            FirstTransactionDate = DateTime.UtcNow.AddMonths(-6),
            LastTransactionDate = DateTime.UtcNow.AddDays(-1),
            TotalTransactions = 50,
            TotalSpent = 5000m,
            AvgAmount = 100m
        };
    }

    private BehavioralData CreateTestBehavioralData(double riskScore)
    {
        return new BehavioralData
        {
            RiskScore = riskScore,
            AnomalyFlags = new List<string>()
        };
    }
}
