using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Pages.Settings;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public List<AlertRule> AlertRules { get; set; } = new();
    public List<Webhook> Webhooks { get; set; } = new();

    public IndexModel(ILogger<IndexModel> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            
            var alertRulesResponse = await client.GetAsync("/api/Settings/alert-rules");
            if (alertRulesResponse.IsSuccessStatusCode)
            {
                AlertRules = await alertRulesResponse.Content.ReadFromJsonAsync<List<AlertRule>>() ?? new();
            }

            var webhooksResponse = await client.GetAsync("/api/Settings/webhooks");
            if (webhooksResponse.IsSuccessStatusCode)
            {
                Webhooks = await webhooksResponse.Content.ReadFromJsonAsync<List<Webhook>>() ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings data");
        }
    }
}
