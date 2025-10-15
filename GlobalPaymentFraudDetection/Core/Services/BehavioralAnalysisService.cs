using GlobalPaymentFraudDetection.Core.Interfaces.Services;
using GlobalPaymentFraudDetection.Models;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;

namespace GlobalPaymentFraudDetection.Core.Services;

public class BehavioralAnalysisService : IBehavioralAnalysisService
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly ILogger<BehavioralAnalysisService> _logger;
    private readonly DatabaseReader? _geoIpReader;

    public BehavioralAnalysisService(
        ICosmosDbService cosmosDbService,
        ILogger<BehavioralAnalysisService> logger,
        IConfiguration configuration)
    {
        _cosmosDbService = cosmosDbService;
        _logger = logger;

        var geoIpDbPath = configuration["GeoIP:DatabasePath"];
        if (!string.IsNullOrEmpty(geoIpDbPath) && File.Exists(geoIpDbPath))
        {
            _geoIpReader = new DatabaseReader(geoIpDbPath);
        }
    }

    public async Task<BehavioralData> AnalyzeTransactionBehaviorAsync(Transaction transaction, UserProfile? userProfile)
    {
        var behavioralData = new BehavioralData
        {
            UserId = transaction.UserId,
            IpAddress = transaction.IpAddress
        };

        behavioralData.GeoLocation = await GetGeoLocationAsync(transaction.IpAddress);
        behavioralData.DeviceInfo = ExtractDeviceFingerprint(transaction);
        behavioralData.Velocity = await CalculateVelocityAsync(transaction.UserId);

        var anomalyFlags = new List<string>();

        if (userProfile != null)
        {
            if (behavioralData.Velocity.TransactionsLast1Hour > 10)
                anomalyFlags.Add("HIGH_VELOCITY_1H");

            if (behavioralData.Velocity.TransactionsLast24Hours > 50)
                anomalyFlags.Add("HIGH_VELOCITY_24H");

            if (behavioralData.Velocity.UniqueDevicesLast24Hours > 5)
                anomalyFlags.Add("MULTIPLE_DEVICES");

            if (behavioralData.Velocity.UniqueIpsLast24Hours > 5)
                anomalyFlags.Add("MULTIPLE_IPS");

            if (transaction.Amount > userProfile.AvgAmount * 3)
                anomalyFlags.Add("UNUSUAL_AMOUNT");

            if (behavioralData.GeoLocation?.IsProxy == true || 
                behavioralData.GeoLocation?.IsVpn == true || 
                behavioralData.GeoLocation?.IsTor == true)
                anomalyFlags.Add("PROXY_VPN_TOR");
        }

        behavioralData.AnomalyFlags = anomalyFlags;
        behavioralData.RiskScore = CalculateRiskScore(behavioralData);

        return behavioralData;
    }

    public async Task<GeoLocationData?> GetGeoLocationAsync(string ipAddress)
    {
        try
        {
            if (_geoIpReader == null)
            {
                return new GeoLocationData();
            }

            var response = await Task.Run(() => _geoIpReader.City(ipAddress));
            
            return new GeoLocationData
            {
                Country = response.Country.Name ?? "Unknown",
                City = response.City.Name ?? "Unknown",
                Latitude = response.Location.Latitude ?? 0,
                Longitude = response.Location.Longitude ?? 0,
                IspName = response.Traits.Isp ?? "Unknown",
                IsProxy = response.Traits.IsAnonymousProxy,
                IsVpn = response.Traits.IsAnonymousVpn,
                IsTor = response.Traits.IsTorExitNode,
                RiskScore = CalculateGeoRiskScore(response)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get geo location for IP: {IpAddress}", ipAddress);
            return null;
        }
    }

    public async Task<TransactionVelocity> CalculateVelocityAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var transactions = await _cosmosDbService.GetUserTransactionsAsync(userId, 1000);

        var last1Hour = transactions.Where(t => t.Timestamp >= now.AddHours(-1)).ToList();
        var last24Hours = transactions.Where(t => t.Timestamp >= now.AddHours(-24)).ToList();
        var last7Days = transactions.Where(t => t.Timestamp >= now.AddDays(-7)).ToList();

        var velocity = new TransactionVelocity
        {
            TransactionsLast1Hour = last1Hour.Count,
            TransactionsLast24Hours = last24Hours.Count,
            TransactionsLast7Days = last7Days.Count,
            AmountLast1Hour = last1Hour.Sum(t => t.Amount),
            AmountLast24Hours = last24Hours.Sum(t => t.Amount),
            UniqueDevicesLast24Hours = last24Hours.Select(t => t.DeviceId).Distinct().Count(),
            UniqueIpsLast24Hours = last24Hours.Select(t => t.IpAddress).Distinct().Count()
        };

        velocity.VelocityScore = CalculateVelocityScore(velocity);

        return velocity;
    }

    private DeviceFingerprint ExtractDeviceFingerprint(Transaction transaction)
    {
        return new DeviceFingerprint
        {
            DeviceId = transaction.DeviceId,
            DeviceType = transaction.DeviceType,
            Browser = transaction.Browser,
            OperatingSystem = transaction.OperatingSystem
        };
    }

    private double CalculateRiskScore(BehavioralData data)
    {
        double score = 0;

        score += data.AnomalyFlags.Count * 10;
        
        if (data.GeoLocation != null)
        {
            score += data.GeoLocation.RiskScore;
        }

        if (data.Velocity != null)
        {
            score += data.Velocity.VelocityScore;
        }

        return Math.Min(score, 100);
    }

    private int CalculateGeoRiskScore(CityResponse response)
    {
        int score = 0;

        if (response.Traits.IsAnonymousProxy) score += 30;
        if (response.Traits.IsAnonymousVpn) score += 25;
        if (response.Traits.IsTorExitNode) score += 40;

        return score;
    }

    private double CalculateVelocityScore(TransactionVelocity velocity)
    {
        double score = 0;

        if (velocity.TransactionsLast1Hour > 10) score += 20;
        if (velocity.TransactionsLast24Hours > 50) score += 15;
        if (velocity.UniqueDevicesLast24Hours > 5) score += 15;
        if (velocity.UniqueIpsLast24Hours > 5) score += 15;

        return Math.Min(score, 100);
    }
}
