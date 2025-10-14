using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public class FraudScoringService : IFraudScoringService
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IOnnxModelService _onnxModelService;
    private readonly IServiceBusService _serviceBusService;
    private readonly IBehavioralAnalysisService _behavioralAnalysisService;
    private readonly ILogger<FraudScoringService> _logger;
    private readonly IConfiguration _configuration;

    public FraudScoringService(
        ICosmosDbService cosmosDbService,
        IOnnxModelService onnxModelService,
        IServiceBusService serviceBusService,
        IBehavioralAnalysisService behavioralAnalysisService,
        ILogger<FraudScoringService> logger,
        IConfiguration configuration)
    {
        _cosmosDbService = cosmosDbService;
        _onnxModelService = onnxModelService;
        _serviceBusService = serviceBusService;
        _behavioralAnalysisService = behavioralAnalysisService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<FraudScoreResponse> ScoreTransactionAsync(Transaction transaction)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var userProfile = await _cosmosDbService.GetUserProfileAsync(transaction.UserId);
            var behavioralData = await _behavioralAnalysisService.AnalyzeTransactionBehaviorAsync(transaction, userProfile);
            var fraudProbability = await _onnxModelService.PredictFraudProbabilityAsync(transaction, userProfile, behavioralData);

            var threshold = _configuration.GetValue<double>("FraudDetection:Threshold", 0.7);
            var isFraudulent = fraudProbability > threshold;
            var decision = isFraudulent ? "DECLINED" : "APPROVED";

            var riskFactors = new Dictionary<string, double>
            {
                { "ModelScore", fraudProbability },
                { "BehavioralRisk", behavioralData.RiskScore / 100.0 },
                { "VelocityRisk", behavioralData.Velocity?.VelocityScore ?? 0 / 100.0 },
                { "AnomalyCount", behavioralData.AnomalyFlags.Count }
            };

            var response = new FraudScoreResponse
            {
                TransactionId = transaction.TransactionId,
                FraudProbability = fraudProbability,
                IsFraudulent = isFraudulent,
                Decision = decision,
                Reason = isFraudulent ? $"High fraud risk: {string.Join(", ", behavioralData.AnomalyFlags)}" : "Low fraud risk",
                ProcessedAt = DateTime.UtcNow,
                RiskFactors = riskFactors,
                ReviewStatus = fraudProbability > 0.5 && fraudProbability <= threshold ? "MANUAL_REVIEW" : "AUTO"
            };

            if (isFraudulent)
            {
                var alert = new FraudAlert
                {
                    TransactionId = transaction.TransactionId,
                    UserId = transaction.UserId,
                    Amount = transaction.Amount,
                    FraudProbability = fraudProbability,
                    AlertType = "HIGH_RISK_TRANSACTION",
                    Severity = fraudProbability > 0.9 ? "CRITICAL" : "HIGH",
                    Reasons = behavioralData.AnomalyFlags
                };

                await _serviceBusService.SendFraudAlertAsync(alert);
            }

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation(
                "Transaction {TransactionId} scored in {Duration}ms. Fraud probability: {Probability}, Decision: {Decision}",
                transaction.TransactionId, duration, fraudProbability, decision);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scoring transaction {TransactionId}", transaction.TransactionId);
            throw;
        }
    }
}
