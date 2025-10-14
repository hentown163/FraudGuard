using GlobalPaymentFraudDetection.Models;
using GlobalPaymentFraudDetection.Infrastructure;

namespace GlobalPaymentFraudDetection.Services;

public class EnsembleModelService : IEnsembleModelService
{
    private readonly IOnnxModelService _onnxModelService;
    private readonly IAdvancedRiskScoringService _riskScoringService;
    private readonly ILogger<EnsembleModelService> _logger;

    private readonly Dictionary<string, double> _modelWeights = new()
    {
        { "OnnxModel", 0.40 },
        { "RuleBasedModel", 0.25 },
        { "StatisticalModel", 0.20 },
        { "BehavioralModel", 0.15 }
    };

    public EnsembleModelService(
        IOnnxModelService onnxModelService,
        IAdvancedRiskScoringService riskScoringService,
        ILogger<EnsembleModelService> logger)
    {
        _onnxModelService = onnxModelService;
        _riskScoringService = riskScoringService;
        _logger = logger;
    }

    public async Task<double> PredictWithEnsembleAsync(
        Transaction transaction, 
        UserProfile? userProfile, 
        BehavioralData behavioralData)
    {
        using var activity = DistributedTracing.ActivitySource.StartFraudDetectionActivity(
            "EnsemblePrediction", transaction.TransactionId);

        try
        {
            var predictions = await GetModelPredictionsAsync(transaction, userProfile, behavioralData);

            var ensembleScore = predictions.Sum(p => 
                _modelWeights.TryGetValue(p.Key, out var weight) ? p.Value * weight : 0);

            activity?.SetTag("ensemble.score", ensembleScore);
            activity?.SetTag("ensemble.model_count", predictions.Count);

            foreach (var prediction in predictions)
            {
                activity?.SetTag($"model.{prediction.Key.ToLower()}", prediction.Value);
            }

            _logger.LogInformation(
                "Ensemble prediction for transaction {TransactionId}: {Score} (Models: {ModelCount})",
                transaction.TransactionId, ensembleScore, predictions.Count);

            return ensembleScore;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ensemble prediction for transaction {TransactionId}", transaction.TransactionId);
            activity?.RecordException(ex);
            throw;
        }
    }

    public async Task<Dictionary<string, double>> GetModelPredictionsAsync(
        Transaction transaction, 
        UserProfile? userProfile, 
        BehavioralData behavioralData)
    {
        var predictions = new Dictionary<string, double>();

        try
        {
            var onnxScore = await _onnxModelService.PredictFraudProbabilityAsync(transaction, userProfile, behavioralData);
            predictions["OnnxModel"] = onnxScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX model prediction failed, using fallback");
            predictions["OnnxModel"] = 0.5;
        }

        try
        {
            var ruleBasedScore = CalculateRuleBasedScore(transaction, behavioralData);
            predictions["RuleBasedModel"] = ruleBasedScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rule-based model prediction failed");
            predictions["RuleBasedModel"] = 0.5;
        }

        try
        {
            var statisticalScore = await CalculateStatisticalScore(transaction);
            predictions["StatisticalModel"] = statisticalScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Statistical model prediction failed");
            predictions["StatisticalModel"] = 0.5;
        }

        try
        {
            var behavioralScore = behavioralData.RiskScore / 100.0;
            predictions["BehavioralModel"] = behavioralScore;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Behavioral model prediction failed");
            predictions["BehavioralModel"] = 0.5;
        }

        return predictions;
    }

    private double CalculateRuleBasedScore(Transaction transaction, BehavioralData behavioralData)
    {
        var score = 0.0;

        if (transaction.Amount > 10000) score += 0.3;
        if (transaction.Amount > 50000) score += 0.4;

        if (behavioralData.AnomalyFlags.Contains("FirstTimeCountry")) score += 0.2;
        if (behavioralData.AnomalyFlags.Contains("HighVelocity")) score += 0.3;
        if (behavioralData.AnomalyFlags.Contains("UnusualAmount")) score += 0.2;
        if (behavioralData.AnomalyFlags.Contains("NewDevice")) score += 0.15;

        var hour = transaction.Timestamp.Hour;
        if (hour >= 1 && hour <= 5) score += 0.15;

        return Math.Min(score, 1.0);
    }

    private async Task<double> CalculateStatisticalScore(Transaction transaction)
    {
        var riskScores = await _riskScoringService.CalculateAllRiskScoresAsync(transaction, null);

        var avgRisk = riskScores.Values.Average();
        var maxRisk = riskScores.Values.Max();

        return (avgRisk * 0.6) + (maxRisk * 0.4);
    }
}
