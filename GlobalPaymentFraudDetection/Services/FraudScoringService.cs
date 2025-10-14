using GlobalPaymentFraudDetection.Models;
using GlobalPaymentFraudDetection.Infrastructure;
using System.Diagnostics;

namespace GlobalPaymentFraudDetection.Services;

public class FraudScoringService : IFraudScoringService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnsembleModelService _ensembleModelService;
    private readonly IServiceBusService _serviceBusService;
    private readonly IBehavioralAnalysisService _behavioralAnalysisService;
    private readonly IAdvancedRiskScoringService _advancedRiskScoringService;
    private readonly IFraudRulesEngine _fraudRulesEngine;
    private readonly ILogger<FraudScoringService> _logger;
    private readonly IConfiguration _configuration;

    public FraudScoringService(
        IUnitOfWork unitOfWork,
        IEnsembleModelService ensembleModelService,
        IServiceBusService serviceBusService,
        IBehavioralAnalysisService behavioralAnalysisService,
        IAdvancedRiskScoringService advancedRiskScoringService,
        IFraudRulesEngine fraudRulesEngine,
        ILogger<FraudScoringService> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _ensembleModelService = ensembleModelService;
        _serviceBusService = serviceBusService;
        _behavioralAnalysisService = behavioralAnalysisService;
        _advancedRiskScoringService = advancedRiskScoringService;
        _fraudRulesEngine = fraudRulesEngine;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<FraudScoreResponse> ScoreTransactionAsync(Transaction transaction)
    {
        var startTime = DateTime.UtcNow;

        using var activity = DistributedTracing.ActivitySource.StartFraudDetectionActivity(
            "ScoreTransaction", transaction.TransactionId);

        try
        {
            activity?.AddFraudTags(transaction.UserId, transaction.Amount, 0);

            var shouldBlock = await _fraudRulesEngine.ShouldBlockTransactionAsync(transaction);
            if (shouldBlock)
            {
                activity?.AddDecisionTag("BLOCKED", "CRITICAL");
                activity?.RecordFraudEvent(DistributedTracing.Events.FraudDetected, new Dictionary<string, object>
                {
                    { "reason", "rule_violation" },
                    { "transaction_id", transaction.TransactionId }
                });

                return new FraudScoreResponse
                {
                    TransactionId = transaction.TransactionId,
                    FraudProbability = 1.0,
                    IsFraudulent = true,
                    Decision = "BLOCKED",
                    Reason = "Transaction blocked by fraud rules",
                    ProcessedAt = DateTime.UtcNow,
                    RiskFactors = new Dictionary<string, double> { { "RuleViolation", 1.0 } },
                    ReviewStatus = "BLOCKED"
                };
            }

            var userProfile = await _unitOfWork.UserProfiles.GetByUserIdAsync(transaction.UserId);
            var behavioralData = await _behavioralAnalysisService.AnalyzeTransactionBehaviorAsync(transaction, userProfile);
            
            var fraudProbability = await _ensembleModelService.PredictWithEnsembleAsync(transaction, userProfile, behavioralData);

            var advancedRiskScores = await _advancedRiskScoringService.CalculateAllRiskScoresAsync(transaction, userProfile);
            
            var ruleViolations = await _fraudRulesEngine.EvaluateRulesAsync(transaction, userProfile);

            var threshold = _configuration.GetValue<double>("FraudDetection:Threshold", 0.7);
            var isFraudulent = fraudProbability > threshold;
            var requiresManualReview = await _fraudRulesEngine.RequiresManualReviewAsync(transaction, fraudProbability);

            var decision = isFraudulent ? "DECLINED" : "APPROVED";
            var reviewStatus = requiresManualReview ? "MANUAL_REVIEW" : "AUTO";

            var riskFactors = new Dictionary<string, double>
            {
                { "EnsembleScore", fraudProbability },
                { "BehavioralRisk", behavioralData.RiskScore / 100.0 },
                { "VelocityRisk", advancedRiskScores.GetValueOrDefault("VelocityRisk", 0) },
                { "DeviceRisk", advancedRiskScores.GetValueOrDefault("DeviceRisk", 0) },
                { "GeolocationRisk", advancedRiskScores.GetValueOrDefault("GeolocationRisk", 0) },
                { "AmountRisk", advancedRiskScores.GetValueOrDefault("AmountRisk", 0) },
                { "TimeRisk", advancedRiskScores.GetValueOrDefault("TimeRisk", 0) },
                { "RuleViolations", ruleViolations.Count }
            };

            activity?.AddFraudTags(transaction.UserId, transaction.Amount, fraudProbability);
            activity?.AddDecisionTag(decision, isFraudulent ? "HIGH" : "LOW");

            var response = new FraudScoreResponse
            {
                TransactionId = transaction.TransactionId,
                FraudProbability = fraudProbability,
                IsFraudulent = isFraudulent,
                Decision = decision,
                Reason = isFraudulent 
                    ? $"High fraud risk: {string.Join(", ", behavioralData.AnomalyFlags.Concat(ruleViolations))}" 
                    : "Low fraud risk",
                ProcessedAt = DateTime.UtcNow,
                RiskFactors = riskFactors,
                ReviewStatus = reviewStatus
            };

            if (isFraudulent || requiresManualReview)
            {
                var alert = new FraudAlert
                {
                    TransactionId = transaction.TransactionId,
                    UserId = transaction.UserId,
                    Amount = transaction.Amount,
                    FraudProbability = fraudProbability,
                    AlertType = isFraudulent ? "HIGH_RISK_TRANSACTION" : "MANUAL_REVIEW_REQUIRED",
                    Severity = fraudProbability > 0.9 ? "CRITICAL" : fraudProbability > 0.7 ? "HIGH" : "MEDIUM",
                    Reasons = behavioralData.AnomalyFlags.Concat(ruleViolations).ToList()
                };

                await _serviceBusService.SendFraudAlertAsync(alert);
                await _unitOfWork.FraudAlerts.AddAsync(alert);

                activity?.RecordFraudEvent(DistributedTracing.Events.AlertSent, new Dictionary<string, object>
                {
                    { "severity", alert.Severity },
                    { "alert_type", alert.AlertType }
                });

                if (requiresManualReview)
                {
                    activity?.RecordFraudEvent(DistributedTracing.Events.ManualReviewTriggered);
                }
            }

            transaction.IsFraudulent = isFraudulent;
            transaction.FraudScore = fraudProbability;
            await _unitOfWork.Transactions.AddAsync(transaction);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            activity?.SetTag("performance.duration_ms", duration);

            _logger.LogInformation(
                "Transaction {TransactionId} scored in {Duration}ms. Fraud probability: {Probability}, Decision: {Decision}, Review: {Review}",
                transaction.TransactionId, duration, fraudProbability, decision, reviewStatus);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scoring transaction {TransactionId}", transaction.TransactionId);
            activity?.RecordException(ex);
            throw;
        }
    }
}
