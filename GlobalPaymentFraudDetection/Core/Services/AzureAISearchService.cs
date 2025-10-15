using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Core.Services;

public class AzureAISearchService : IAzureAISearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly ILogger<AzureAISearchService> _logger;
    private const string IndexName = "fraud-transactions-index";

    public AzureAISearchService(IConfiguration configuration, ILogger<AzureAISearchService> logger)
    {
        _logger = logger;
        var endpoint = configuration["AzureAISearch:Endpoint"] ?? 
                       Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT") ?? 
                       "https://placeholder-search.search.windows.net";
        
        var apiKey = configuration["AzureAISearch:ApiKey"] ?? 
                     Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY") ?? 
                     "placeholder-key";

        var credential = new AzureKeyCredential(apiKey);
        _indexClient = new SearchIndexClient(new Uri(endpoint), credential);
        _searchClient = _indexClient.GetSearchClient(IndexName);
    }

    public async Task<bool> IndexTransactionAsync(Transaction transaction)
    {
        try
        {
            var searchDocument = new SearchDocument
            {
                ["id"] = transaction.Id,
                ["userId"] = transaction.UserId,
                ["amount"] = transaction.Amount,
                ["currency"] = transaction.Currency,
                ["timestamp"] = transaction.Timestamp,
                ["ipAddress"] = transaction.IpAddress ?? "",
                ["deviceId"] = transaction.DeviceId ?? "",
                ["merchantName"] = transaction.MerchantName ?? "",
                ["fraudScore"] = transaction.FraudScore,
                ["status"] = transaction.Status ?? "",
                ["paymentGateway"] = transaction.PaymentGateway ?? "",
                ["searchText"] = $"{transaction.UserId} {transaction.MerchantName} {transaction.Amount} {transaction.Currency}"
            };

            await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new[] { searchDocument }));
            _logger.LogInformation("Transaction {TransactionId} indexed successfully", transaction.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing transaction {TransactionId}", transaction.Id);
            return false;
        }
    }

    public async Task<List<Transaction>> SearchTransactionsAsync(string query, int maxResults = 10)
    {
        try
        {
            var options = new SearchOptions
            {
                Size = maxResults,
                IncludeTotalCount = true
            };

            var response = await _searchClient.SearchAsync<SearchDocument>(query, options);
            var transactions = new List<Transaction>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                transactions.Add(MapSearchDocumentToTransaction(result.Document));
            }

            _logger.LogInformation("Search returned {Count} results for query: {Query}", transactions.Count, query);
            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching transactions with query: {Query}", query);
            return new List<Transaction>();
        }
    }

    public async Task<List<Transaction>> SemanticSearchAsync(string naturalLanguageQuery, int maxResults = 10)
    {
        try
        {
            var options = new SearchOptions
            {
                Size = maxResults,
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new SemanticSearchOptions
                {
                    SemanticConfigurationName = "fraud-semantic-config"
                }
            };

            var response = await _searchClient.SearchAsync<SearchDocument>(naturalLanguageQuery, options);
            var transactions = new List<Transaction>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                transactions.Add(MapSearchDocumentToTransaction(result.Document));
            }

            _logger.LogInformation("Semantic search returned {Count} results", transactions.Count);
            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in semantic search with query: {Query}", naturalLanguageQuery);
            return await SearchTransactionsAsync(naturalLanguageQuery, maxResults);
        }
    }

    public async Task<bool> DeleteTransactionIndexAsync(string transactionId)
    {
        try
        {
            var batch = IndexDocumentsBatch.Delete("id", new[] { transactionId });
            await _searchClient.IndexDocumentsAsync(batch);
            _logger.LogInformation("Transaction {TransactionId} deleted from index", transactionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transaction {TransactionId} from index", transactionId);
            return false;
        }
    }

    public async Task<bool> CreateOrUpdateIndexAsync()
    {
        try
        {
            var fieldBuilder = new FieldBuilder();
            var searchFields = new List<SearchField>
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SearchableField("userId") { IsFilterable = true, IsSortable = true },
                new SimpleField("amount", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
                new SearchableField("currency") { IsFilterable = true },
                new SimpleField("timestamp", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                new SearchableField("ipAddress") { IsFilterable = true },
                new SearchableField("deviceId") { IsFilterable = true },
                new SearchableField("merchantName") { IsFilterable = true },
                new SimpleField("fraudScore", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
                new SearchableField("status") { IsFilterable = true },
                new SearchableField("paymentGateway") { IsFilterable = true },
                new SearchableField("searchText") { IsSearchable = true }
            };

            var index = new SearchIndex(IndexName, searchFields);
            
            await _indexClient.CreateOrUpdateIndexAsync(index);
            _logger.LogInformation("Search index {IndexName} created/updated successfully", IndexName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating search index");
            return false;
        }
    }

    private Transaction MapSearchDocumentToTransaction(SearchDocument document)
    {
        return new Transaction
        {
            Id = document.TryGetValue("id", out object? id) ? id?.ToString() ?? "" : "",
            UserId = document.TryGetValue("userId", out object? userId) ? userId?.ToString() ?? "" : "",
            Amount = document.TryGetValue("amount", out object? amount) ? Convert.ToDecimal(amount) : 0,
            Currency = document.TryGetValue("currency", out object? currency) ? currency?.ToString() ?? "USD" : "USD",
            Timestamp = document.TryGetValue("timestamp", out object? timestamp) ? Convert.ToDateTime(timestamp) : DateTime.UtcNow,
            IpAddress = document.TryGetValue("ipAddress", out object? ip) ? ip?.ToString() : null,
            DeviceId = document.TryGetValue("deviceId", out object? deviceId) ? deviceId?.ToString() : null,
            MerchantName = document.TryGetValue("merchantName", out object? merchant) ? merchant?.ToString() : null,
            FraudScore = document.TryGetValue("fraudScore", out object? score) ? Convert.ToDouble(score) : 0,
            Status = document.TryGetValue("status", out object? status) ? status?.ToString() : null,
            PaymentGateway = document.TryGetValue("paymentGateway", out object? gateway) ? gateway?.ToString() : null
        };
    }
}
