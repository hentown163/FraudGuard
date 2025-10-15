using Azure;
using Azure.AI.OpenAI;
using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;
using System.Text.Json;

namespace GlobalPaymentFraudDetection.Core.Services;

public class AzureOpenAIService : IAzureOpenAIService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly string _deploymentName;

    public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger)
    {
        _logger = logger;
        
        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? 
                       Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? 
                       "https://placeholder-openai.openai.azure.com";
        
        var apiKey = configuration["AzureOpenAI:ApiKey"] ?? 
                     Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? 
                     "placeholder-key";
        
        _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? 
                         Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? 
                         "gpt-4";

        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        _chatClient = azureClient.GetChatClient(_deploymentName);
    }

    public async Task<string> AnalyzeFraudPatternAsync(Transaction transaction, FraudScoreResponse fraudScore)
    {
        try
        {
            var prompt = $@"You are a fraud detection expert. Analyze the following transaction and provide insights:

Transaction Details:
- ID: {transaction.Id}
- User: {transaction.UserId}
- Amount: {transaction.Amount} {transaction.Currency}
- Timestamp: {transaction.Timestamp:yyyy-MM-dd HH:mm:ss}
- IP Address: {transaction.IpAddress}
- Device: {transaction.DeviceId}
- Merchant: {transaction.MerchantName}
- Payment Gateway: {transaction.PaymentGateway}

Fraud Analysis:
- Fraud Score: {fraudScore.FraudScore:P}
- Risk Level: {fraudScore.RiskLevel}
- Decision: {fraudScore.Decision}
- Risk Factors: {string.Join(", ", fraudScore.RiskFactors)}

Please provide:
1. A summary of why this transaction is flagged as {fraudScore.RiskLevel} risk
2. Key suspicious patterns identified
3. Recommended actions for the fraud analyst
4. Similar fraud patterns to watch for

Keep the response concise and actionable.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are an expert fraud detection analyst specializing in payment fraud patterns."),
                new UserChatMessage(prompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            var analysis = response.Value.Content[0].Text;
            
            _logger.LogInformation("Generated fraud analysis for transaction {TransactionId}", transaction.Id);
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing fraud pattern for transaction {TransactionId}", transaction.Id);
            return "Unable to generate AI analysis at this time. Please review the transaction manually.";
        }
    }

    public async Task<string> GenerateFraudSummaryAsync(List<Transaction> transactions)
    {
        try
        {
            var summary = new StringBuilder();
            summary.AppendLine("Transaction Summary:");
            summary.AppendLine($"- Total Transactions: {transactions.Count}");
            summary.AppendLine($"- Total Amount: {transactions.Sum(t => t.Amount):C}");
            summary.AppendLine($"- High Risk: {transactions.Count(t => t.FraudScore > 0.7)}");
            summary.AppendLine($"- Date Range: {transactions.Min(t => t.Timestamp):yyyy-MM-dd} to {transactions.Max(t => t.Timestamp):yyyy-MM-dd}");

            var prompt = $@"You are a fraud detection expert. Analyze the following transaction summary and provide insights:

{summary}

Top Suspicious Transactions:
{string.Join("\n", transactions.OrderByDescending(t => t.FraudScore).Take(5).Select(t => 
    $"- {t.Id}: {t.Amount:C} from {t.UserId} (Score: {t.FraudScore:P})"))}

Please provide:
1. Overall fraud trend analysis
2. Key patterns and anomalies detected
3. Recommended actions
4. Risk mitigation strategies

Be concise and focus on actionable insights.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are an expert fraud detection analyst providing executive summaries."),
                new UserChatMessage(prompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            var analysis = response.Value.Content[0].Text;
            
            _logger.LogInformation("Generated fraud summary for {Count} transactions", transactions.Count);
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating fraud summary");
            return "Unable to generate fraud summary at this time.";
        }
    }

    public async Task<string> GetFraudInsightsAsync(string query)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an expert fraud detection analyst. Provide insights and answer questions about fraud detection, 
payment security, fraud patterns, and risk mitigation strategies. Base your responses on industry best practices and real-world fraud scenarios."),
                new UserChatMessage(query)
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            var insights = response.Value.Content[0].Text;
            
            _logger.LogInformation("Generated fraud insights for query: {Query}", query);
            return insights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fraud insights");
            return "Unable to retrieve insights at this time.";
        }
    }

    public async Task<List<string>> DetectAnomaliesWithAIAsync(Transaction transaction, List<Transaction> historicalTransactions)
    {
        try
        {
            var avgAmount = historicalTransactions.Average(t => t.Amount);
            var avgFraudScore = historicalTransactions.Average(t => t.FraudScore);
            
            var prompt = $@"Analyze this transaction for anomalies compared to user's historical behavior:

Current Transaction:
- Amount: {transaction.Amount:C}
- Timestamp: {transaction.Timestamp:yyyy-MM-dd HH:mm:ss}
- IP: {transaction.IpAddress}
- Device: {transaction.DeviceId}
- Merchant: {transaction.MerchantName}

Historical Profile:
- Average Amount: {avgAmount:C}
- Average Fraud Score: {avgFraudScore:P}
- Total Historical Transactions: {historicalTransactions.Count}
- Unique Merchants: {historicalTransactions.Select(t => t.MerchantName).Distinct().Count()}

Identify specific anomalies as a list of findings. Be specific and actionable.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a fraud detection AI. Identify anomalies and return them as a concise bulleted list."),
                new UserChatMessage(prompt)
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            var anomaliesText = response.Value.Content[0].Text;
            
            var anomalies = anomaliesText.Split('\n')
                .Where(line => line.Trim().StartsWith("-") || line.Trim().StartsWith("•"))
                .Select(line => line.Trim().TrimStart('-', '•').Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            
            _logger.LogInformation("Detected {Count} AI anomalies for transaction {TransactionId}", anomalies.Count, transaction.Id);
            return anomalies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting anomalies with AI");
            return new List<string>();
        }
    }

    public async Task<string> ChatWithFraudAssistantAsync(string userMessage, List<Transaction> context)
    {
        try
        {
            var contextSummary = "";
            if (context.Any())
            {
                contextSummary = $@"

Context - Recent Transactions:
{string.Join("\n", context.Take(10).Select(t => 
    $"- {t.Id}: {t.Amount:C} from {t.UserId} at {t.Timestamp:yyyy-MM-dd HH:mm} (Score: {t.FraudScore:P})"))}";
            }

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(@"You are an AI-powered fraud detection assistant. Help users investigate transactions, 
understand fraud patterns, and make informed decisions. Provide clear, actionable advice based on the transaction context provided."),
                new UserChatMessage($"{userMessage}{contextSummary}")
            };

            var response = await _chatClient.CompleteChatAsync(messages);
            var assistantResponse = response.Value.Content[0].Text;
            
            _logger.LogInformation("Fraud assistant responded to user message");
            return assistantResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fraud assistant chat");
            return "I'm having trouble processing your request right now. Please try again later.";
        }
    }
}
