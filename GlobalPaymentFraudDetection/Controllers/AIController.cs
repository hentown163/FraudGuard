using Microsoft.AspNetCore.Mvc;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly IAzureOpenAIService _openAIService;
    private readonly IAzureAISearchService _searchService;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<AIController> _logger;

    public AIController(
        IAzureOpenAIService openAIService,
        IAzureAISearchService searchService,
        ICosmosDbService cosmosDbService,
        ILogger<AIController> logger)
    {
        _openAIService = openAIService;
        _searchService = searchService;
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        try
        {
            var context = request.TransactionIds != null && request.TransactionIds.Any()
                ? await Task.WhenAll(request.TransactionIds.Select(id => _cosmosDbService.GetTransactionByIdAsync(id)))
                : Array.Empty<Transaction>();

            var response = await _openAIService.ChatWithFraudAssistantAsync(
                request.Message, 
                context.Where(t => t != null).ToList()!
            );

            return Ok(new { response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI chat");
            return StatusCode(500, new { error = "Failed to process chat request" });
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] bool semantic = false)
    {
        try
        {
            var transactions = semantic 
                ? await _searchService.SemanticSearchAsync(q)
                : await _searchService.SearchTransactionsAsync(q);

            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI search");
            return StatusCode(500, new { error = "Search failed" });
        }
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeTransaction([FromBody] AnalyzeRequest request)
    {
        try
        {
            var transaction = await _cosmosDbService.GetTransactionByIdAsync(request.TransactionId);
            if (transaction == null)
            {
                return NotFound(new { error = "Transaction not found" });
            }

            var historicalTransactions = await _cosmosDbService.GetUserTransactionHistoryAsync(transaction.UserId, 30);
            var anomalies = await _openAIService.DetectAnomaliesWithAIAsync(transaction, historicalTransactions);

            return Ok(new { 
                transactionId = transaction.Id,
                anomalies,
                riskScore = transaction.FraudScore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing transaction");
            return StatusCode(500, new { error = "Analysis failed" });
        }
    }

    [HttpPost("insights")]
    public async Task<IActionResult> GetInsights([FromBody] InsightsRequest request)
    {
        try
        {
            var insights = await _openAIService.GetFraudInsightsAsync(request.Query);
            return Ok(new { insights });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting insights");
            return StatusCode(500, new { error = "Failed to get insights" });
        }
    }

    [HttpPost("summary")]
    public async Task<IActionResult> GenerateSummary([FromBody] SummaryRequest request)
    {
        try
        {
            var transactions = await Task.WhenAll(
                request.TransactionIds.Select(id => _cosmosDbService.GetTransactionByIdAsync(id))
            );

            var validTransactions = transactions.Where(t => t != null).ToList()!;
            var summary = await _openAIService.GenerateFraudSummaryAsync(validTransactions);

            return Ok(new { summary });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating summary");
            return StatusCode(500, new { error = "Failed to generate summary" });
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<string>? TransactionIds { get; set; }
}

public class AnalyzeRequest
{
    public string TransactionId { get; set; } = string.Empty;
}

public class InsightsRequest
{
    public string Query { get; set; } = string.Empty;
}

public class SummaryRequest
{
    public List<string> TransactionIds { get; set; } = new();
}
