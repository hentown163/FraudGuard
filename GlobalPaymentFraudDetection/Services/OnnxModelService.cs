using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public class OnnxModelService : IOnnxModelService
{
    private readonly InferenceSession? _session;
    private readonly ILogger<OnnxModelService> _logger;
    private readonly bool _modelLoaded;

    public OnnxModelService(ILogger<OnnxModelService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var modelPath = configuration["ONNX:ModelPath"] ?? "wwwroot/onnx/fraud_model.onnx";

        if (File.Exists(modelPath))
        {
            try
            {
                _session = new InferenceSession(modelPath);
                _modelLoaded = true;
                _logger.LogInformation("ONNX model loaded successfully from {ModelPath}", modelPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ONNX model from {ModelPath}", modelPath);
                _modelLoaded = false;
            }
        }
        else
        {
            _logger.LogWarning("ONNX model not found at {ModelPath}. Using fallback scoring.", modelPath);
            _modelLoaded = false;
        }
    }

    public async Task<double> PredictFraudProbabilityAsync(
        Transaction transaction, 
        UserProfile? userProfile, 
        BehavioralData behavioralData)
    {
        if (!_modelLoaded || _session == null)
        {
            return await Task.FromResult(CalculateFallbackScore(transaction, userProfile, behavioralData));
        }

        try
        {
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("amount", 
                    new DenseTensor<float>(new[] { (float)transaction.Amount }, new[] { 1 })),
                NamedOnnxValue.CreateFromTensor("user_id_hash", 
                    new DenseTensor<float>(new[] { HashString(transaction.UserId) }, new[] { 1 })),
                NamedOnnxValue.CreateFromTensor("total_transactions", 
                    new DenseTensor<float>(new[] { userProfile?.TotalTransactions ?? 0 }, new[] { 1 })),
                NamedOnnxValue.CreateFromTensor("avg_amount", 
                    new DenseTensor<float>(new[] { (float)(userProfile?.AvgAmount ?? 0) }, new[] { 1 })),
                NamedOnnxValue.CreateFromTensor("suspicious_flags", 
                    new DenseTensor<float>(new[] { userProfile?.SuspiciousFlags ?? 0 }, new[] { 1 })),
                NamedOnnxValue.CreateFromTensor("velocity_score", 
                    new DenseTensor<float>(new[] { (float)behavioralData.Velocity?.VelocityScore ?? 0 }, new[] { 1 })),
                NamedOnnxValue.CreateFromTensor("behavioral_risk", 
                    new DenseTensor<float>(new[] { (float)behavioralData.RiskScore }, new[] { 1 }))
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>().ToArray();
            return await Task.FromResult(Math.Min(output[0], 1.0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ONNX prediction failed, using fallback");
            return await Task.FromResult(CalculateFallbackScore(transaction, userProfile, behavioralData));
        }
    }

    private double CalculateFallbackScore(Transaction transaction, UserProfile? userProfile, BehavioralData behavioralData)
    {
        double score = 0;

        score += behavioralData.RiskScore / 100.0 * 0.3;
        score += (behavioralData.Velocity?.VelocityScore ?? 0) / 100.0 * 0.2;
        score += behavioralData.AnomalyFlags.Count * 0.1;

        if (userProfile != null)
        {
            if (userProfile.SuspiciousFlags > 0)
                score += 0.2;
            
            if (userProfile.ChargebackCount > 0)
                score += 0.15;

            if (transaction.Amount > userProfile.AvgAmount * 5)
                score += 0.1;
        }

        return Math.Min(score, 1.0);
    }

    private float HashString(string str)
    {
        return Math.Abs(str.GetHashCode() % 1000000) / 1000000f;
    }
}
