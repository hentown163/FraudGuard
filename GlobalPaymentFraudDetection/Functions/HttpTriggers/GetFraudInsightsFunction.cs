using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using System.Net;

namespace GlobalPaymentFraudDetection.Functions.HttpTriggers;

public class GetFraudInsightsFunction
{
    private readonly ILogger<GetFraudInsightsFunction> _logger;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IFraudAlertRepository _fraudAlertRepository;

    public GetFraudInsightsFunction(
        ILogger<GetFraudInsightsFunction> logger,
        ITransactionRepository transactionRepository,
        IFraudAlertRepository fraudAlertRepository)
    {
        _logger = logger;
        _transactionRepository = transactionRepository;
        _fraudAlertRepository = fraudAlertRepository;
    }

    [Function("GetFraudInsights")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "fraud/insights")] HttpRequestData req)
    {
        _logger.LogInformation("GetFraudInsights function processing request");

        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var days = query["days"] != null ? int.Parse(query["days"]!) : 7;
            var startDate = DateTime.UtcNow.AddDays(-days);

            var allTransactions = await _transactionRepository.GetTransactionsByDateRangeAsync(startDate, DateTime.UtcNow);
            var alerts = await _fraudAlertRepository.GetAlertsByDateRangeAsync(startDate, DateTime.UtcNow);

            var totalTransactions = allTransactions.Count();
            var fraudulentTransactions = allTransactions.Count(t => t.Status == "Declined");
            var pendingReview = allTransactions.Count(t => t.Status == "Pending");
            var avgFraudScore = allTransactions.Any() ? allTransactions.Average(t => t.FraudScore) : 0;

            var topRiskFactors = allTransactions
                .Where(t => t.RiskFactors != null && t.RiskFactors.Any())
                .SelectMany(t => t.RiskFactors!)
                .GroupBy(r => r)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new { factor = g.Key, count = g.Count() });

            var gatewayDistribution = allTransactions
                .GroupBy(t => t.Gateway)
                .Select(g => new { gateway = g.Key, count = g.Count(), fraudRate = g.Count(t => t.Status == "Declined") * 100.0 / g.Count() });

            var insights = new
            {
                period = new { days, startDate, endDate = DateTime.UtcNow },
                summary = new
                {
                    totalTransactions,
                    fraudulentTransactions,
                    pendingReview,
                    fraudRate = totalTransactions > 0 ? fraudulentTransactions * 100.0 / totalTransactions : 0,
                    avgFraudScore = Math.Round(avgFraudScore, 3)
                },
                topRiskFactors,
                gatewayDistribution,
                alerts = new
                {
                    total = alerts.Count(),
                    open = alerts.Count(a => a.Status == "Open"),
                    resolved = alerts.Count(a => a.Status == "Resolved"),
                    critical = alerts.Count(a => a.Severity == "Critical")
                }
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(insights);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating fraud insights");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
            return errorResponse;
        }
    }
}
