using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using GlobalPaymentFraudDetection.Models;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Functions.Models;

namespace GlobalPaymentFraudDetection.Functions.Triggers;

public class FraudAnalysisHttpTrigger
{
    private readonly ILogger<FraudAnalysisHttpTrigger> _logger;
    private readonly IFraudScoringService _fraudScoringService;
    private readonly ICosmosDbService _cosmosDbService;

    public FraudAnalysisHttpTrigger(
        ILogger<FraudAnalysisHttpTrigger> logger,
        IFraudScoringService fraudScoringService,
        ICosmosDbService cosmosDbService)
    {
        _logger = logger;
        _fraudScoringService = fraudScoringService;
        _cosmosDbService = cosmosDbService;
    }

    [Function("AnalyzeFraud")]
    public async Task<HttpResponseData> AnalyzeFraud(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "fraud/analyze")] HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken cancellationToken)
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

            var transaction = MapToTransaction(analysisRequest);
            
            var fraudResult = await _fraudScoringService.ScoreTransactionAsync(transaction);

            await _cosmosDbService.StoreTransactionAsync(transaction);

            var response = new FraudAnalysisResponse
            {
                TransactionId = transaction.TransactionId,
                FraudScore = fraudResult.FraudProbability,
                RiskLevel = DetermineRiskLevel(fraudResult.FraudProbability),
                Decision = fraudResult.Decision,
                RiskFactors = fraudResult.RiskFactors.Keys.ToList(),
                AnalyzedAt = fraudResult.ProcessedAt
            };

            _logger.LogInformation(
                "Fraud analysis completed for transaction {TransactionId}: Score={FraudScore}, Decision={Decision}",
                transaction.TransactionId, fraudResult.FraudProbability, fraudResult.Decision);

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
        FunctionContext executionContext,
        CancellationToken cancellationToken)
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

            _logger.LogInformation("Processing batch of {Count} transactions", requests.Count);

            var tasks = requests.Select(async r =>
            {
                var transaction = MapToTransaction(r);
                var result = await _fraudScoringService.ScoreTransactionAsync(transaction);
                await _cosmosDbService.StoreTransactionAsync(transaction);

                return new FraudAnalysisResponse
                {
                    TransactionId = transaction.TransactionId,
                    FraudScore = result.FraudProbability,
                    RiskLevel = DetermineRiskLevel(result.FraudProbability),
                    Decision = result.Decision,
                    RiskFactors = result.RiskFactors.Keys.ToList(),
                    AnalyzedAt = result.ProcessedAt
                };
            });

            var responses = await Task.WhenAll(tasks);

            _logger.LogInformation("Bulk analysis completed for {Count} transactions", responses.Length);

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

    private Transaction MapToTransaction(FraudAnalysisRequest request)
    {
        return new Transaction
        {
            TransactionId = request.TransactionId,
            UserId = request.UserEmail,
            Amount = request.Amount,
            Currency = request.Currency,
            IpAddress = request.IpAddress,
            PaymentGateway = request.PaymentGateway,
            DeviceId = request.DeviceFingerprint,
            Timestamp = DateTime.UtcNow,
            Status = "PENDING",
            Metadata = request.Metadata ?? new Dictionary<string, string>()
        };
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
}
