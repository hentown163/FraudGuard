using Azure;
using Azure.AI.AnomalyDetector;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Services;

public class AnomalyDetectionService : IAnomalyDetectionService
{
    private readonly ILogger<AnomalyDetectionService> _logger;
    private readonly string _endpoint;
    private readonly string _apiKey;

    public AnomalyDetectionService(IConfiguration configuration, ILogger<AnomalyDetectionService> logger)
    {
        _logger = logger;
        _endpoint = configuration["AzureAnomalyDetector:Endpoint"] ?? 
                   Environment.GetEnvironmentVariable("AZURE_ANOMALY_DETECTOR_ENDPOINT") ?? 
                   "https://placeholder-anomaly.cognitiveservices.azure.com";
        
        _apiKey = configuration["AzureAnomalyDetector:ApiKey"] ?? 
                 Environment.GetEnvironmentVariable("AZURE_ANOMALY_DETECTOR_API_KEY") ?? 
                 "placeholder-key";
    }

    public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(List<TransactionDataPoint> dataPoints)
    {
        try
        {
            if (dataPoints.Count < 12)
            {
                _logger.LogWarning("Insufficient data points for anomaly detection (need at least 12, got {Count})", dataPoints.Count);
                return new AnomalyDetectionResult
                {
                    IsAnomaly = false,
                    AnomalyScore = 0,
                    Severity = "Unknown",
                    DetectedPatterns = new List<string> { "Insufficient historical data" }
                };
            }

            var client = new AnomalyDetectorClient(new Uri(_endpoint), new AzureKeyCredential(_apiKey));
            
            var detectionOptions = new UnivariateDetectionOptions(dataPoints.Select(dp => new TimeSeriesPoint(dp.Value)
            {
                Timestamp = dp.Timestamp
            }).ToList())
            {
                Granularity = TimeGranularity.Hourly,
                Sensitivity = 95
            };

            var response = await client.DetectUnivariateEntireSeriesAsync(detectionOptions);
            var lastPoint = response.Value.IsAnomaly.Last();
            var lastScore = response.Value.ExpectedValues.Last();

            var patterns = new List<string>();
            if (response.Value.IsPositiveAnomaly.Last())
            {
                patterns.Add("Unusually high transaction value detected");
            }
            if (response.Value.IsNegativeAnomaly.Last())
            {
                patterns.Add("Unusually low transaction value detected");
            }

            var result = new AnomalyDetectionResult
            {
                IsAnomaly = lastPoint,
                AnomalyScore = Math.Abs(dataPoints.Last().Value - lastScore),
                Severity = lastPoint ? (Math.Abs(dataPoints.Last().Value - lastScore) > lastScore * 0.5 ? "High" : "Medium") : "Low",
                DetectedPatterns = patterns
            };

            _logger.LogInformation("Anomaly detection completed: IsAnomaly={IsAnomaly}, Score={Score}", result.IsAnomaly, result.AnomalyScore);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in anomaly detection");
            return new AnomalyDetectionResult
            {
                IsAnomaly = false,
                AnomalyScore = 0,
                Severity = "Error",
                DetectedPatterns = new List<string> { "Anomaly detection service unavailable" }
            };
        }
    }

    public async Task<bool> IsTransactionAnomalousAsync(Transaction transaction, List<Transaction> historicalTransactions)
    {
        try
        {
            if (!historicalTransactions.Any())
            {
                _logger.LogInformation("No historical data for user {UserId}, cannot detect anomalies", transaction.UserId);
                return false;
            }

            var dataPoints = historicalTransactions
                .OrderBy(t => t.Timestamp)
                .Select(t => new TransactionDataPoint
                {
                    Timestamp = t.Timestamp,
                    Value = (double)t.Amount
                })
                .Concat(new[] { new TransactionDataPoint { Timestamp = transaction.Timestamp, Value = (double)transaction.Amount } })
                .ToList();

            if (dataPoints.Count < 12)
            {
                var avgAmount = historicalTransactions.Average(t => t.Amount);
                var stdDev = Math.Sqrt(historicalTransactions.Average(t => Math.Pow((double)(t.Amount - avgAmount), 2)));
                var zScore = Math.Abs((double)(transaction.Amount - avgAmount) / (stdDev == 0 ? 1 : stdDev));
                
                var isAnomaly = zScore > 3;
                _logger.LogInformation("Statistical anomaly detection: ZScore={ZScore}, IsAnomaly={IsAnomaly}", zScore, isAnomaly);
                return isAnomaly;
            }

            var result = await DetectAnomaliesAsync(dataPoints);
            return result.IsAnomaly;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if transaction is anomalous");
            return false;
        }
    }
}
