using Microsoft.AspNetCore.Mvc;
using GlobalPaymentFraudDetection.Models;
using System.Security.Cryptography;
using System.Text;

namespace GlobalPaymentFraudDetection.Controllers;

/// <summary>
/// WARNING: This controller uses in-memory storage for demonstration purposes only.
/// In production, use a persistent database and implement proper encryption for secrets.
/// Webhook secrets and API keys should be hashed and stored securely.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ILogger<SettingsController> _logger;
    private static List<AlertRule> _alertRules = new()
    {
        new AlertRule
        {
            Name = "High Value Transaction",
            Description = "Alert when transaction > $5,000",
            Condition = "Amount > 5000",
            Threshold = 5000,
            Action = "Email",
            IsActive = true
        },
        new AlertRule
        {
            Name = "Multiple Failed Attempts",
            Description = "Alert when > 3 failed attempts in 10 minutes",
            Condition = "FailedAttempts > 3",
            Threshold = 3,
            Action = "Email,SMS",
            IsActive = true
        },
        new AlertRule
        {
            Name = "Geographic Anomaly",
            Description = "Alert on transactions from unusual countries",
            Condition = "UnusualLocation == true",
            Threshold = 1,
            Action = "Email",
            IsActive = false
        }
    };

    private static List<Webhook> _webhooks = new()
    {
        new Webhook
        {
            Name = "Slack Notifications",
            Url = "https://hooks.slack.com/services/...",
            Events = new List<string> { "fraud.detected", "transaction.declined" },
            Secret = GenerateSecret(),
            IsActive = true
        }
    };

    private static List<ApiKey> _apiKeys = new()
    {
        new ApiKey
        {
            Name = "Production Key",
            Key = GenerateApiKey(),
            CreatedAt = DateTime.UtcNow.AddDays(-14),
            LastUsedAt = DateTime.UtcNow.AddHours(-2),
            Environment = "production"
        }
    };

    public SettingsController(ILogger<SettingsController> logger)
    {
        _logger = logger;
    }

    // Alert Rules Endpoints
    [HttpGet("alert-rules")]
    public ActionResult<List<AlertRule>> GetAlertRules()
    {
        return Ok(_alertRules);
    }

    [HttpPost("alert-rules")]
    public ActionResult<AlertRule> CreateAlertRule([FromBody] AlertRule alertRule)
    {
        alertRule.Id = Guid.NewGuid().ToString();
        alertRule.CreatedAt = DateTime.UtcNow;
        _alertRules.Add(alertRule);
        _logger.LogInformation("Created new alert rule: {RuleName}", alertRule.Name);
        return CreatedAtAction(nameof(GetAlertRules), new { id = alertRule.Id }, alertRule);
    }

    [HttpPut("alert-rules/{id}")]
    public ActionResult<AlertRule> UpdateAlertRule(string id, [FromBody] AlertRule alertRule)
    {
        var existing = _alertRules.FirstOrDefault(r => r.Id == id);
        if (existing == null)
            return NotFound();

        existing.Name = alertRule.Name;
        existing.Description = alertRule.Description;
        existing.Condition = alertRule.Condition;
        existing.Threshold = alertRule.Threshold;
        existing.Action = alertRule.Action;
        existing.IsActive = alertRule.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation("Updated alert rule: {RuleName}", existing.Name);
        return Ok(existing);
    }

    [HttpDelete("alert-rules/{id}")]
    public ActionResult DeleteAlertRule(string id)
    {
        var rule = _alertRules.FirstOrDefault(r => r.Id == id);
        if (rule == null)
            return NotFound();

        _alertRules.Remove(rule);
        _logger.LogInformation("Deleted alert rule: {RuleName}", rule.Name);
        return NoContent();
    }

    // Webhook Endpoints
    [HttpGet("webhooks")]
    public ActionResult<List<object>> GetWebhooks()
    {
        var safeWebhooks = _webhooks.Select(w => new
        {
            w.Id,
            w.Name,
            w.Url,
            w.Events,
            SecretHint = MaskSecret(w.Secret),
            w.IsActive,
            w.CreatedAt,
            w.LastTriggeredAt
        }).ToList();
        return Ok(safeWebhooks);
    }

    [HttpPost("webhooks")]
    public ActionResult<object> CreateWebhook([FromBody] Webhook webhook)
    {
        webhook.Id = Guid.NewGuid().ToString();
        webhook.CreatedAt = DateTime.UtcNow;
        webhook.Secret = GenerateSecret();
        _webhooks.Add(webhook);
        _logger.LogInformation("Created new webhook: {WebhookName}", webhook.Name);
        
        return Ok(new
        {
            webhook.Id,
            webhook.Name,
            webhook.Url,
            webhook.Events,
            Secret = webhook.Secret,
            SecretHint = MaskSecret(webhook.Secret),
            webhook.IsActive,
            webhook.CreatedAt,
            Message = "⚠️ Save this secret now. You won't be able to see it again!"
        });
    }

    [HttpPut("webhooks/{id}")]
    public ActionResult<object> UpdateWebhook(string id, [FromBody] Webhook webhook)
    {
        var existing = _webhooks.FirstOrDefault(w => w.Id == id);
        if (existing == null)
            return NotFound();

        existing.Name = webhook.Name;
        existing.Url = webhook.Url;
        existing.Events = webhook.Events;
        existing.IsActive = webhook.IsActive;

        _logger.LogInformation("Updated webhook: {WebhookName}", existing.Name);
        
        return Ok(new
        {
            existing.Id,
            existing.Name,
            existing.Url,
            existing.Events,
            SecretHint = MaskSecret(existing.Secret),
            existing.IsActive,
            existing.CreatedAt,
            existing.LastTriggeredAt
        });
    }

    [HttpDelete("webhooks/{id}")]
    public ActionResult DeleteWebhook(string id)
    {
        var webhook = _webhooks.FirstOrDefault(w => w.Id == id);
        if (webhook == null)
            return NotFound();

        _webhooks.Remove(webhook);
        _logger.LogInformation("Deleted webhook: {WebhookName}", webhook.Name);
        return NoContent();
    }

    // API Key Endpoints
    [HttpGet("api-keys")]
    public ActionResult<List<object>> GetApiKeys()
    {
        var maskedKeys = _apiKeys.Select(k => new
        {
            k.Id,
            k.Name,
            Key = MaskApiKey(k.Key),
            k.CreatedAt,
            k.LastUsedAt,
            k.IsActive,
            k.Environment
        }).ToList();
        return Ok(maskedKeys);
    }

    [HttpPost("api-keys")]
    public ActionResult<object> CreateApiKey([FromBody] ApiKeyRequest request)
    {
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Key = GenerateApiKey(),
            CreatedAt = DateTime.UtcNow,
            Environment = request.Environment ?? "development"
        };

        _apiKeys.Add(apiKey);
        _logger.LogInformation("Created new API key: {KeyName}", apiKey.Name);

        return Ok(new
        {
            apiKey.Id,
            apiKey.Name,
            Key = apiKey.Key,
            MaskedKey = MaskApiKey(apiKey.Key),
            apiKey.CreatedAt,
            apiKey.Environment,
            Message = "⚠️ Save this key now. You won't be able to see it again!"
        });
    }

    [HttpDelete("api-keys/{id}")]
    public ActionResult RevokeApiKey(string id)
    {
        var apiKey = _apiKeys.FirstOrDefault(k => k.Id == id);
        if (apiKey == null)
            return NotFound();

        _apiKeys.Remove(apiKey);
        _logger.LogInformation("Revoked API key: {KeyName}", apiKey.Name);
        return NoContent();
    }

    private static string GenerateApiKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(40);
        var base64 = Convert.ToBase64String(bytes).Replace("+", "X").Replace("/", "Y").Replace("=", "");
        var key = base64.Length >= 40 ? base64[..40] : base64.PadRight(40, 'Z');
        return "fpd_pk_" + key;
    }

    private static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var base64 = Convert.ToBase64String(bytes).Replace("+", "X").Replace("/", "Y").Replace("=", "");
        return base64.Length >= 32 ? base64[..32] : base64.PadRight(32, 'Z');
    }

    private static string MaskApiKey(string key)
    {
        if (key.Length < 16)
            return key;

        var prefix = key[..10];
        return prefix + new string('*', 28);
    }

    private static string MaskSecret(string secret)
    {
        if (secret.Length < 8)
            return new string('*', secret.Length);

        var prefix = secret[..4];
        var suffix = secret[^4..];
        return prefix + new string('*', secret.Length - 8) + suffix;
    }
}

public class ApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Environment { get; set; }
}
