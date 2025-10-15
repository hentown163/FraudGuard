using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using System.Net;
using System.Text.Json;

namespace GlobalPaymentFraudDetection.Functions.HttpTriggers;

public class AnalyzeFraudTransactionFunction
{
    private readonly ILogger<AnalyzeFraudTransactionFunction> _logger;
    private readonly IFraudScoringService _fraudScoringService;
    private readonly IBehavioralAnalysisService _behavioralAnalysisService;
    private readonly IServiceBusService _serviceBusService;

    public AnalyzeFraudTransactionFunction(
        ILogger<AnalyzeFraudTransactionFunction> logger,
        IFraudScoringService fraudScoringService,
        IBehavioralAnalysisService behavioralAnalysisService,
        IServiceBusService serviceBusService)
    {
        _logger = logger;
        _fraudScoringService = fraudScoringService;
        _behavioralAnalysisService = behavioralAnalysisService;
        _serviceBusService = serviceBusService;
    }

    [Function("AnalyzeFraudTransaction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "fraud/analyze")] HttpRequestData req)
    {
        _logger.LogInformation("AnalyzeFraudTransaction function processing request");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var transaction = JsonSerializer.Deserialize<Transaction>(requestBody);

            if (transaction == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid transaction data" });
                return badResponse;
            }

            var behavioralData = await _behavioralAnalysisService.AnalyzeTransactionAsync(transaction);
            var fraudScore = await _fraudScoringService.ScoreTransactionAsync(transaction, behavioralData);

            if (fraudScore.Score >= 0.7)
            {
                var alert = new FraudAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    TransactionId = transaction.Id,
                    UserId = transaction.UserId,
                    Severity = fraudScore.Score >= 0.9 ? "Critical" : "High",
                    Type = "Automated Detection",
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow,
                    RiskFactors = fraudScore.RiskFactors
                };

                await _serviceBusService.SendFraudAlertAsync(alert);
                _logger.LogWarning($"High-risk transaction detected: {transaction.Id} with score {fraudScore.Score}");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(fraudScore);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing transaction");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
            return errorResponse;
        }
    }
}
