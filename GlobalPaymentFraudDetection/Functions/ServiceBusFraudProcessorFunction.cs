using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using System.Text.Json;

namespace GlobalPaymentFraudDetection.Functions;

public class ServiceBusFraudProcessorFunction
{
    private readonly IFraudScoringService _fraudScoringService;
    private readonly IAzureAISearchService _searchService;
    private readonly IAnomalyDetectionService _anomalyDetectionService;
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<ServiceBusFraudProcessorFunction> _logger;

    public ServiceBusFraudProcessorFunction(
        IFraudScoringService fraudScoringService,
        IAzureAISearchService searchService,
        IAnomalyDetectionService anomalyDetectionService,
        ICosmosDbService cosmosDbService,
        ILogger<ServiceBusFraudProcessorFunction> logger)
    {
        _fraudScoringService = fraudScoringService;
        _searchService = searchService;
        _anomalyDetectionService = anomalyDetectionService;
        _cosmosDbService = cosmosDbService;
        _logger = logger;
    }

    [Function("ProcessFraudAlert")]
    public async Task ProcessFraudAlert(
        [ServiceBusTrigger("fraud-alerts", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Processing fraud alert from Service Bus");

        try
        {
            var transaction = JsonSerializer.Deserialize<Transaction>(message);
            
            if (transaction == null)
            {
                _logger.LogWarning("Invalid transaction data received");
                return;
            }

            var historicalTransactions = await _cosmosDbService.GetUserTransactionHistoryAsync(transaction.UserId, 30);
            
            var isAnomalous = await _anomalyDetectionService.IsTransactionAnomalousAsync(
                transaction, 
                historicalTransactions
            );

            if (isAnomalous)
            {
                transaction.FraudScore = Math.Max(transaction.FraudScore, 0.85);
                _logger.LogWarning("Anomaly detected for transaction {TransactionId}, updated fraud score to {Score}", 
                    transaction.Id, transaction.FraudScore);
            }

            await _searchService.IndexTransactionAsync(transaction);
            await _cosmosDbService.SaveTransactionAsync(transaction);

            _logger.LogInformation("Successfully processed fraud alert for transaction {TransactionId}", transaction.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fraud alert");
            throw;
        }
    }

    [Function("BatchProcessTransactions")]
    public async Task BatchProcessTransactions(
        [ServiceBusTrigger("transaction-batch", Connection = "ServiceBusConnection")] string message)
    {
        _logger.LogInformation("Processing transaction batch from Service Bus");

        try
        {
            var transactions = JsonSerializer.Deserialize<List<Transaction>>(message);
            
            if (transactions == null || !transactions.Any())
            {
                _logger.LogWarning("No transactions in batch");
                return;
            }

            foreach (var transaction in transactions)
            {
                var fraudScore = await _fraudScoringService.ScoreTransactionAsync(transaction);
                transaction.FraudScore = fraudScore.FraudScore;
                transaction.Status = fraudScore.Decision;
                
                await _searchService.IndexTransactionAsync(transaction);
            }

            _logger.LogInformation("Successfully processed batch of {Count} transactions", transactions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction batch");
            throw;
        }
    }
}
