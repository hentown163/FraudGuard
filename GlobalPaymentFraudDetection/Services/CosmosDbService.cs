using Microsoft.Azure.Cosmos;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly Container _userProfileContainer;
    private readonly Container _transactionContainer;

    public CosmosDbService(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseName = configuration["Cosmos:DatabaseName"] ?? "FraudDetection";
        var database = cosmosClient.GetDatabase(databaseName);
        _userProfileContainer = database.GetContainer("UserProfiles");
        _transactionContainer = database.GetContainer("Transactions");
    }

    public async Task<UserProfile?> GetUserProfileAsync(string userId)
    {
        try
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userId")
                .WithParameter("@userId", userId);
            
            var iterator = _userProfileContainer.GetItemQueryIterator<UserProfile>(query);
            var response = await iterator.ReadNextAsync();
            
            return response.FirstOrDefault();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpsertUserProfileAsync(UserProfile profile)
    {
        await _userProfileContainer.UpsertItemAsync(profile, new PartitionKey(profile.UserId));
    }

    public async Task<List<Transaction>> GetUserTransactionsAsync(string userId, int limit = 100)
    {
        var query = new QueryDefinition(
            "SELECT TOP @limit * FROM c WHERE c.UserId = @userId ORDER BY c.Timestamp DESC")
            .WithParameter("@userId", userId)
            .WithParameter("@limit", limit);

        var iterator = _transactionContainer.GetItemQueryIterator<Transaction>(query);
        var transactions = new List<Transaction>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            transactions.AddRange(response);
        }

        return transactions;
    }
}
