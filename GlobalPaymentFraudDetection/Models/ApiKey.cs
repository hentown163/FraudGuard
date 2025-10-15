namespace GlobalPaymentFraudDetection.Models;

public class ApiKey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = "fpd_pk_";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string Environment { get; set; } = "development";
}
