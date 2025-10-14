using GlobalPaymentFraudDetection.Services;
using GlobalPaymentFraudDetection.Hubs;
using Microsoft.Azure.Cosmos;
using Azure.Messaging.ServiceBus;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSignalR();

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

builder.Services.AddScoped<IKeyVaultService, KeyVaultService>();
builder.Services.AddScoped<ICosmosDbService, CosmosDbService>();
builder.Services.AddScoped<IServiceBusService, ServiceBusService>();
builder.Services.AddScoped<IBehavioralAnalysisService, BehavioralAnalysisService>();
builder.Services.AddScoped<IOnnxModelService, OnnxModelService>();
builder.Services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();
builder.Services.AddScoped<IFraudScoringService, FraudScoringService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"] ?? Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "sk_test_placeholder";

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<FraudDetectionHub>("/fraudhub");

app.Run();
