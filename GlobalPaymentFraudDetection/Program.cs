using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Core.Services;
using GlobalPaymentFraudDetection.Hubs;
using GlobalPaymentFraudDetection.Infrastructure;
using GlobalPaymentFraudDetection.Middleware;
using GlobalPaymentFraudDetection.Validators;
using Microsoft.Azure.Cosmos;
using Azure.Messaging.ServiceBus;
using Stripe;
using System.Diagnostics;
using FluentValidation;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"] 
    ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
        options.EnableAdaptiveSampling = true;
        options.EnableQuickPulseMetricStream = true;
    });
}
else
{
    builder.Services.AddApplicationInsightsTelemetry();
}

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<TransactionValidator>();

builder.Services.AddSignalR();

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["Cosmos:ConnectionString"] ?? Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
    return new CosmosClient(connectionString ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
});

builder.Services.AddSingleton<ServiceBusClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config["ServiceBus:ConnectionString"] ?? Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING");
    return new ServiceBusClient(connectionString ?? "Endpoint=sb://localhost");
});

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IKeyVaultService, KeyVaultService>();
builder.Services.AddScoped<ICosmosDbService, CosmosDbService>();
builder.Services.AddScoped<IServiceBusService, ServiceBusService>();
builder.Services.AddScoped<IBehavioralAnalysisService, BehavioralAnalysisService>();
builder.Services.AddScoped<IOnnxModelService, OnnxModelService>();
builder.Services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IAdvancedRiskScoringService, AdvancedRiskScoringService>();
builder.Services.AddScoped<IEnsembleModelService, EnsembleModelService>();
builder.Services.AddScoped<IFraudRulesEngine, FraudRulesEngine>();
builder.Services.AddScoped<ISiftScienceService, SiftScienceService>();
builder.Services.AddScoped<IFraudScoringService, FraudScoringService>();

ActivitySource.AddActivityListener(new ActivityListener
{
    ShouldListenTo = source => source.Name == DistributedTracing.ServiceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
    ActivityStarted = activity => Console.WriteLine($"Activity Started: {activity.DisplayName}"),
    ActivityStopped = activity => Console.WriteLine($"Activity Stopped: {activity.DisplayName} ({activity.Duration.TotalMilliseconds}ms)")
});

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"] ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "sk_test_placeholder";

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<FraudDetectionHub>("/fraudhub");

app.Run();
