using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Core.Services;
using GlobalPaymentFraudDetection.Core.Interfaces.Repositories;
using GlobalPaymentFraudDetection.Core.Repositories;

namespace GlobalPaymentFraudDetection.Functions;

public class FunctionsProgram
{
    public static void Main()
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IConfiguration>(context.Configuration);

                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();

                services.AddSingleton<CosmosClient>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var connectionString = config["Cosmos:ConnectionString"] 
                        ?? Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING")
                        ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
                    return new CosmosClient(connectionString);
                });

                services.AddSingleton<ServiceBusClient>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var connectionString = config["ServiceBus:ConnectionString"] 
                        ?? Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING")
                        ?? "Endpoint=sb://localhost";
                    return new ServiceBusClient(connectionString);
                });

                services.AddSingleton<SecretClient>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var vaultUri = config["KeyVault:VaultUri"] 
                        ?? Environment.GetEnvironmentVariable("KEYVAULT_URI")
                        ?? "https://your-keyvault.vault.azure.net/";
                    return new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
                });

                services.AddScoped<ITransactionRepository>(sp =>
                {
                    var cosmosClient = sp.GetRequiredService<CosmosClient>();
                    var config = sp.GetRequiredService<IConfiguration>();
                    var databaseName = config["Cosmos:DatabaseName"] ?? "FraudDetection";
                    var container = cosmosClient.GetDatabase(databaseName).GetContainer("Transactions");
                    var logger = sp.GetRequiredService<ILogger<CosmosRepository<Models.Transaction>>>();
                    return new TransactionRepository(container, logger);
                });

                services.AddScoped<IUserProfileRepository>(sp =>
                {
                    var cosmosClient = sp.GetRequiredService<CosmosClient>();
                    var config = sp.GetRequiredService<IConfiguration>();
                    var databaseName = config["Cosmos:DatabaseName"] ?? "FraudDetection";
                    var container = cosmosClient.GetDatabase(databaseName).GetContainer("UserProfiles");
                    var logger = sp.GetRequiredService<ILogger<CosmosRepository<Models.UserProfile>>>();
                    return new UserProfileRepository(container, logger);
                });

                services.AddScoped<IFraudAlertRepository>(sp =>
                {
                    var cosmosClient = sp.GetRequiredService<CosmosClient>();
                    var config = sp.GetRequiredService<IConfiguration>();
                    var databaseName = config["Cosmos:DatabaseName"] ?? "FraudDetection";
                    var container = cosmosClient.GetDatabase(databaseName).GetContainer("FraudAlerts");
                    var logger = sp.GetRequiredService<ILogger<CosmosRepository<Models.FraudAlert>>>();
                    return new FraudAlertRepository(container, logger);
                });

                services.AddScoped<ICosmosDbService, CosmosDbService>();
                services.AddScoped<IServiceBusService, ServiceBusService>();
                services.AddScoped<IKeyVaultService, KeyVaultService>();
                services.AddScoped<IFraudScoringService, FraudScoringService>();
                services.AddScoped<IBehavioralAnalysisService, BehavioralAnalysisService>();
                services.AddScoped<IOnnxModelService, OnnxModelService>();
                services.AddScoped<INotificationService, NotificationService>();
                services.AddScoped<IAdvancedRiskScoringService, AdvancedRiskScoringService>();
                services.AddScoped<IEnsembleModelService, EnsembleModelService>();
                services.AddScoped<IFraudRulesEngine, FraudRulesEngine>();
                services.AddScoped<ISiftScienceService, SiftScienceService>();
            })
            .Build();

        host.Run();
    }
}
