using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using GlobalPaymentFraudDetection.Functions.Models;

namespace GlobalPaymentFraudDetection.Functions.Triggers;

public class FraudAnalysisHttpTrigger
{
    private readonly ILogger<FraudAnalysisHttpTrigger> _logger;

    public FraudAnalysisHttpTrigger(ILogger<FraudAnalysisHttpTrigger> logger)
    {
        _logger = logger;
    }

    [Function("AnalyzeFraud")]
    public async Task<HttpResponseData> AnalyzeFraud(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "fraud/analyze")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Fraud analysis request received");

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Request body cannot be empty");
                return badRequest;
            }

            var analysisRequest = JsonSerializer.Deserialize<FraudAnalysisRequest>(requestBody, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (analysisRequest == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid request format");
                return badRequest;
            }

            var fraudScore = CalculateFraudScore(analysisRequest);
            var riskLevel = DetermineRiskLevel(fraudScore);
            var decision = MakeDecision(fraudScore);
            var riskFactors = IdentifyRiskFactors(analysisRequest, fraudScore);

            var response = new FraudAnalysisResponse
            {
                TransactionId = analysisRequest.TransactionId,
                FraudScore = fraudScore,
                RiskLevel = riskLevel,
                Decision = decision,
                RiskFactors = riskFactors,
                AnalyzedAt = DateTime.UtcNow
            };

            _logger.LogInformation(
                "Fraud analysis completed for transaction {TransactionId}: Score={FraudScore}, Decision={Decision}",
                analysisRequest.TransactionId, fraudScore, decision);

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(response);
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fraud analysis request");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("BulkAnalyze")]
    public async Task<HttpResponseData> BulkAnalyze(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "fraud/bulk-analyze")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("Bulk fraud analysis request received");

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            var requests = JsonSerializer.Deserialize<List<FraudAnalysisRequest>>(requestBody ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (requests == null || !requests.Any())
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("No transactions to analyze");
                return badRequest;
            }

            var responses = requests.Select(r => new FraudAnalysisResponse
            {
                TransactionId = r.TransactionId,
                FraudScore = CalculateFraudScore(r),
                RiskLevel = DetermineRiskLevel(CalculateFraudScore(r)),
                Decision = MakeDecision(CalculateFraudScore(r)),
                RiskFactors = IdentifyRiskFactors(r, CalculateFraudScore(r)),
                AnalyzedAt = DateTime.UtcNow
            }).ToList();

            _logger.LogInformation("Bulk analysis completed for {Count} transactions", responses.Count);

            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            await httpResponse.WriteAsJsonAsync(responses);
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bulk fraud analysis");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    private double CalculateFraudScore(FraudAnalysisRequest request)
    {
        double score = 0.0;

        if (request.Amount > 1000) score += 0.3;
        if (request.Amount > 5000) score += 0.2;

        if (request.IpAddress.StartsWith("192.168.") || request.IpAddress.StartsWith("10."))
            score += 0.1;

        if (string.IsNullOrEmpty(request.DeviceFingerprint))
            score += 0.15;

        if (request.Metadata?.ContainsKey("velocity_check") == true)
            score += 0.25;

        return Math.Min(score, 1.0);
    }

    private string DetermineRiskLevel(double score)
    {
        return score switch
        {
            >= 0.7 => "Critical",
            >= 0.5 => "High",
            >= 0.3 => "Medium",
            _ => "Low"
        };
    }

    private string MakeDecision(double score)
    {
        return score switch
        {
            >= 0.7 => "Decline",
            >= 0.5 => "Manual Review",
            _ => "Approve"
        };
    }

    private List<string> IdentifyRiskFactors(FraudAnalysisRequest request, double score)
    {
        var factors = new List<string>();

        if (request.Amount > 5000)
            factors.Add("High transaction amount");

        if (string.IsNullOrEmpty(request.DeviceFingerprint))
            factors.Add("Missing device fingerprint");

        if (request.IpAddress.StartsWith("192.168.") || request.IpAddress.StartsWith("10."))
            factors.Add("Private IP address detected");

        if (score >= 0.7)
            factors.Add("Critical fraud score threshold exceeded");

        return factors;
    }
}
