using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GlobalPaymentFraudDetection.Pages.Transactions;

public class DetailsModel : PageModel
{
    public string TransactionId { get; set; } = string.Empty;
    public string UserId { get; set; } = "user_12345";
    public decimal Amount { get; set; } = 599.99m;
    public string Currency { get; set; } = "USD";
    public string PaymentGateway { get; set; } = "Stripe";
    public string PaymentMethod { get; set; } = "Credit Card";
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string Status { get; set; } = "APPROVED";
    public double FraudProbability { get; set; } = 0.35;
    public string Decision { get; set; } = "APPROVED";
    public string ReviewStatus { get; set; } = "AUTO";
    public string Reason { get; set; } = "Low fraud risk";
    public Dictionary<string, double> RiskFactors { get; set; } = new();
    public string IpAddress { get; set; } = "192.168.1.1";
    public string City { get; set; } = "San Francisco";
    public string Country { get; set; } = "USA";
    public string DeviceId { get; set; } = "device_abc123";
    public string DeviceType { get; set; } = "Desktop";
    public string Browser { get; set; } = "Chrome";
    public string OperatingSystem { get; set; } = "Windows 11";

    public void OnGet(string id)
    {
        TransactionId = id ?? Guid.NewGuid().ToString();
        RiskFactors = new Dictionary<string, double>
        {
            { "Model Score", 0.35 },
            { "Behavioral Risk", 0.20 },
            { "Velocity Risk", 0.15 },
            { "Anomaly Count", 0 }
        };
    }

    public IActionResult OnPost(string transactionId, string reviewDecision, string reviewNotes)
    {
        return RedirectToPage("/Dashboard/Index");
    }
}
