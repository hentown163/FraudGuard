namespace GlobalPaymentFraudDetection.Models;

public class BehavioralData
{
    public string UserId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public GeoLocationData? GeoLocation { get; set; }
    public DeviceFingerprint? DeviceInfo { get; set; }
    public TransactionVelocity? Velocity { get; set; }
    public List<string> AnomalyFlags { get; set; } = new();
    public double RiskScore { get; set; }
}

public class GeoLocationData
{
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string IspName { get; set; } = string.Empty;
    public bool IsProxy { get; set; }
    public bool IsVpn { get; set; }
    public bool IsTor { get; set; }
    public int RiskScore { get; set; }
}

public class DeviceFingerprint
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string BrowserVersion { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string ScreenResolution { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public bool CookiesEnabled { get; set; }
    public bool JavaScriptEnabled { get; set; }
}

public class TransactionVelocity
{
    public int TransactionsLast1Hour { get; set; }
    public int TransactionsLast24Hours { get; set; }
    public int TransactionsLast7Days { get; set; }
    public decimal AmountLast1Hour { get; set; }
    public decimal AmountLast24Hours { get; set; }
    public int UniqueDevicesLast24Hours { get; set; }
    public int UniqueIpsLast24Hours { get; set; }
    public double VelocityScore { get; set; }
}
