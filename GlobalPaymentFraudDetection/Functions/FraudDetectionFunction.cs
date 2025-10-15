using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using System.Net;
using System.Text.Json;

namespace GlobalPaymentFraudDetection.Functions;

public class FraudDetectionFunction
{
    private readonly IFraudScoringService _fraudScoringService;
    private readonly IAzureAISearchService _searchService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly ILogger<FraudDetectionFunction> _logger;

    public FraudDetectionFunction(
        IFraudScoringService fraudScoringService,
        IAzureAISearchService searchService,
        IAzureOpenAIService openAIService,
        ILogger<FraudDetectionFunction> logger)
    {
        _fraudScoringService = fraudScoringService;
        _searchService = searchService;
        _openAIService = openAIService;
        _logger = logger;
    }

    [Function("AnalyzeFraudTransaction")]
    public async Task<HttpResponseData> AnalyzeFraudTransaction(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing fraud analysis request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var transaction = JsonSerializer.Deserialize<Transaction>(requestBody);

            if (transaction == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid transaction data");
                return badResponse;
            }

            var fraudScore = await _fraudScoringService.ScoreTransactionAsync(transaction);
            await _searchService.IndexTransactionAsync(transaction);
            var aiAnalysis = await _openAIService.AnalyzeFraudPatternAsync(transaction, fraudScore);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var result = new
            {
                transaction = transaction,
                fraudScore = fraudScore,
                aiAnalysis = aiAnalysis
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing fraud transaction");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("SearchTransactions")]
    public async Task<HttpResponseData> SearchTransactions(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("Processing transaction search request");

        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query).Get("q") ?? "";
            var useSemanticSearch = System.Web.HttpUtility.ParseQueryString(req.Url.Query).Get("semantic") == "true";

            List<Transaction> transactions;
            if (useSemanticSearch)
            {
                transactions = await _searchService.SemanticSearchAsync(query);
            }
            else
            {
                transactions = await _searchService.SearchTransactionsAsync(query);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(transactions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching transactions");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }

    [Function("GetFraudInsights")]
    public async Task<HttpResponseData> GetFraudInsights(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        _logger.LogInformation("Processing fraud insights request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<FraudInsightsRequest>(requestBody);

            if (data == null || string.IsNullOrWhiteSpace(data.Query))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Query is required");
                return badResponse;
            }

            var insights = await _openAIService.GetFraudInsightsAsync(data.Query);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { insights }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fraud insights");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            return errorResponse;
        }
    }
}

public class FraudInsightsRequest
{
    public string Query { get; set; } = string.Empty;
}
