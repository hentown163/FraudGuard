using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using System.Net;
using System.Text.Json;

namespace GlobalPaymentFraudDetection.Functions.HttpTriggers;

public class SearchTransactionsFunction
{
    private readonly ILogger<SearchTransactionsFunction> _logger;
    private readonly ITransactionRepository _transactionRepository;

    public SearchTransactionsFunction(
        ILogger<SearchTransactionsFunction> logger,
        ITransactionRepository transactionRepository)
    {
        _logger = logger;
        _transactionRepository = transactionRepository;
    }

    [Function("SearchTransactions")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "fraud/transactions/search")] HttpRequestData req)
    {
        _logger.LogInformation("SearchTransactions function processing request");

        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var userId = query["userId"];
            var status = query["status"];
            var minScore = query["minScore"] != null ? double.Parse(query["minScore"]!) : 0.0;
            var startDate = query["startDate"] != null ? DateTime.Parse(query["startDate"]!) : DateTime.UtcNow.AddDays(-30);
            var endDate = query["endDate"] != null ? DateTime.Parse(query["endDate"]!) : DateTime.UtcNow;

            var transactions = await _transactionRepository.SearchTransactionsAsync(
                userId, status, minScore, startDate, endDate);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                count = transactions.Count(),
                transactions = transactions.OrderByDescending(t => t.Timestamp).Take(100)
            });
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching transactions");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
            return errorResponse;
        }
    }
}
