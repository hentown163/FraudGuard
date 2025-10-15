using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            .ConfigureServices(services =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();

                services.AddSingleton<ICosmosDbService, CosmosDbService>();
                services.AddSingleton<IServiceBusService, ServiceBusService>();
                services.AddSingleton<IKeyVaultService, KeyVaultService>();
                services.AddSingleton<IFraudScoringService, FraudScoringService>();
                services.AddSingleton<IBehavioralAnalysisService, BehavioralAnalysisService>();
                services.AddSingleton<IOnnxModelService, OnnxModelService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<IAdvancedRiskScoringService, AdvancedRiskScoringService>();
                services.AddSingleton<IEnsembleModelService, EnsembleModelService>();
                services.AddSingleton<IFraudRulesEngine, FraudRulesEngine>();
                services.AddSingleton<ISiftScienceService, SiftScienceService>();

                services.AddScoped<ITransactionRepository, TransactionRepository>();
                services.AddScoped<IUserProfileRepository, UserProfileRepository>();
                services.AddScoped<IFraudAlertRepository, FraudAlertRepository>();
            })
            .Build();

        host.Run();
    }
}
