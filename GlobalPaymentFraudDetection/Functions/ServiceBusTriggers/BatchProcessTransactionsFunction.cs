using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Models;
using System.Text.Json;

namespace GlobalPaymentFraudDetection.Functions.ServiceBusTriggers;

public class BatchProcessTransactionsFunction
{
    private readonly ILogger<BatchProcessTransactionsFunction> _logger;
    private readonly IFraudScoringService _fraudScoringService;
    private readonly IBehavioralAnalysisService _behavioralAnalysisService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IServiceBusService _serviceBusService;

    public BatchProcessTransactionsFunction(
        ILogger<BatchProcessTransactionsFunction> logger,
        IFraudScoringService fraudScoringService,
        IBehavioralAnalysisService behavioralAnalysisService,
        ITransactionRepository transactionRepository,
        IServiceBusService serviceBusService)
    {
        _logger = logger;
        _fraudScoringService = fraudScoringService;
        _behavioralAnalysisService = behavioralAnalysisService;
        _transactionRepository = transactionRepository;
        _serviceBusService = serviceBusService;
    }

    [Function("BatchProcessTransactions")]
    public async Task Run(
        [ServiceBusTrigger("transaction-batch", Connection = "ServiceBusConnection")] string[] messages)
    {
        _logger.LogInformation($"Processing batch of {messages.Length} transactions");

        var processedCount = 0;
        var fraudDetectedCount = 0;

        try
        {
            var tasks = messages.Select(async messageBody =>
            {
                try
                {
                    var transaction = JsonSerializer.Deserialize<Transaction>(messageBody);
                    if (transaction == null)
                    {
                        _logger.LogWarning("Invalid transaction in batch");
                        return;
                    }

                    var behavioralData = await _behavioralAnalysisService.AnalyzeTransactionAsync(transaction);
                    var fraudScore = await _fraudScoringService.ScoreTransactionAsync(transaction, behavioralData);

                    transaction.FraudScore = fraudScore.Score;
                    transaction.RiskFactors = fraudScore.RiskFactors;
                    transaction.Status = fraudScore.Decision;

                    await _transactionRepository.UpdateAsync(transaction);

                    Interlocked.Increment(ref processedCount);

                    if (fraudScore.Score >= 0.7)
                    {
                        Interlocked.Increment(ref fraudDetectedCount);

                        var alert = new FraudAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            TransactionId = transaction.Id,
                            UserId = transaction.UserId,
                            Severity = fraudScore.Score >= 0.9 ? "Critical" : "High",
                            Type = "Batch Detection",
                            Status = "Open",
                            CreatedAt = DateTime.UtcNow,
                            RiskFactors = fraudScore.RiskFactors
                        };

                        await _serviceBusService.SendFraudAlertAsync(alert);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing individual transaction in batch");
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                $"Batch processing complete. Processed: {processedCount}/{messages.Length}, Fraud detected: {fraudDetectedCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction batch");
            throw;
        }
    }
}
