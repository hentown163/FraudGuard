using Microsoft.AspNetCore.Mvc;
using GlobalPaymentFraudDetection.Models;

namespace GlobalPaymentFraudDetection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(ILogger<AnalyticsController> logger)
    {
        _logger = logger;
    }

    [HttpGet("fraud-heatmap")]
    public async Task<IActionResult> GetFraudHeatmapData(
        [FromQuery] int days = 30,
        [FromQuery] double minFraudScore = 0.5)
    {
        try
        {
            // For demo purposes, generate realistic sample data
            // In production, this would query actual fraud data from Cosmos DB
            var heatmapData = GenerateFraudHeatmapData();

            return Ok(new
            {
                success = true,
                data = heatmapData,
                metadata = new
                {
                    totalFraudulent = heatmapData.Count,
                    dateRange = new
                    {
                        from = DateTime.UtcNow.AddDays(-days),
                        to = DateTime.UtcNow
                    },
                    minFraudScore = minFraudScore
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fraud heatmap data");
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to retrieve fraud heatmap data"
            });
        }
    }

    [HttpGet("fraud-stats-by-country")]
    public async Task<IActionResult> GetFraudStatsByCountry([FromQuery] int days = 30)
    {
        try
        {
            var countryStats = GenerateCountryStats();

            return Ok(new
            {
                success = true,
                data = countryStats,
                metadata = new
                {
                    totalCountries = countryStats.Count,
                    dateRange = new
                    {
                        from = DateTime.UtcNow.AddDays(-days),
                        to = DateTime.UtcNow
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fraud stats by country");
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to retrieve country fraud statistics"
            });
        }
    }

    private List<FraudHeatmapPoint> GenerateFraudHeatmapData()
    {
        // Generate realistic fraud hotspot data
        var random = new Random();
        var heatmapPoints = new List<FraudHeatmapPoint>();

        // High fraud activity regions
        var hotspots = new[]
        {
            // North America
            new { Name = "New York, USA", Lat = 40.7128, Lng = -74.0060, Intensity = 0.9 },
            new { Name = "Los Angeles, USA", Lat = 34.0522, Lng = -118.2437, Intensity = 0.85 },
            new { Name = "Miami, USA", Lat = 25.7617, Lng = -80.1918, Intensity = 0.78 },
            new { Name = "Chicago, USA", Lat = 41.8781, Lng = -87.6298, Intensity = 0.72 },
            new { Name = "Toronto, Canada", Lat = 43.6532, Lng = -79.3832, Intensity = 0.65 },
            
            // Europe
            new { Name = "London, UK", Lat = 51.5074, Lng = -0.1278, Intensity = 0.88 },
            new { Name = "Paris, France", Lat = 48.8566, Lng = 2.3522, Intensity = 0.75 },
            new { Name = "Berlin, Germany", Lat = 52.5200, Lng = 13.4050, Intensity = 0.68 },
            new { Name = "Amsterdam, Netherlands", Lat = 52.3676, Lng = 4.9041, Intensity = 0.82 },
            new { Name = "Moscow, Russia", Lat = 55.7558, Lng = 37.6173, Intensity = 0.91 },
            
            // Asia
            new { Name = "Hong Kong", Lat = 22.3193, Lng = 114.1694, Intensity = 0.95 },
            new { Name = "Singapore", Lat = 1.3521, Lng = 103.8198, Intensity = 0.87 },
            new { Name = "Tokyo, Japan", Lat = 35.6762, Lng = 139.6503, Intensity = 0.71 },
            new { Name = "Mumbai, India", Lat = 19.0760, Lng = 72.8777, Intensity = 0.84 },
            new { Name = "Dubai, UAE", Lat = 25.2048, Lng = 55.2708, Intensity = 0.89 },
            
            // South America
            new { Name = "São Paulo, Brazil", Lat = -23.5505, Lng = -46.6333, Intensity = 0.79 },
            new { Name = "Buenos Aires, Argentina", Lat = -34.6037, Lng = -58.3816, Intensity = 0.73 },
            
            // Africa
            new { Name = "Lagos, Nigeria", Lat = 6.5244, Lng = 3.3792, Intensity = 0.93 },
            new { Name = "Johannesburg, South Africa", Lat = -26.2041, Lng = 28.0473, Intensity = 0.76 },
            
            // Oceania
            new { Name = "Sydney, Australia", Lat = -33.8688, Lng = 151.2093, Intensity = 0.69 }
        };

        foreach (var hotspot in hotspots)
        {
            // Add multiple fraud points around each hotspot
            int fraudCount = (int)(hotspot.Intensity * 50) + random.Next(10, 30);
            
            for (int i = 0; i < fraudCount; i++)
            {
                // Add some randomness around the hotspot location
                double latOffset = (random.NextDouble() - 0.5) * 2.0; // ±1 degree
                double lngOffset = (random.NextDouble() - 0.5) * 2.0;
                
                heatmapPoints.Add(new FraudHeatmapPoint
                {
                    Latitude = hotspot.Lat + latOffset,
                    Longitude = hotspot.Lng + lngOffset,
                    Intensity = hotspot.Intensity + (random.NextDouble() - 0.5) * 0.2,
                    FraudScore = Math.Round(hotspot.Intensity + (random.NextDouble() - 0.5) * 0.15, 2),
                    Amount = Math.Round(random.Next(100, 10000) + random.NextDouble() * 1000, 2),
                    Country = hotspot.Name.Split(',').Last().Trim(),
                    City = hotspot.Name.Split(',').First().Trim(),
                    TransactionId = $"txn_{Guid.NewGuid().ToString().Substring(0, 8)}",
                    Timestamp = DateTime.UtcNow.AddHours(-random.Next(0, 720)) // Last 30 days
                });
            }
        }

        return heatmapPoints;
    }

    private List<CountryFraudStats> GenerateCountryStats()
    {
        return new List<CountryFraudStats>
        {
            new CountryFraudStats { Country = "Nigeria", CountryCode = "NG", FraudCount = 1247, TotalAmount = 2847563.45m, RiskLevel = "Critical" },
            new CountryFraudStats { Country = "Hong Kong", CountryCode = "HK", FraudCount = 982, TotalAmount = 3125847.89m, RiskLevel = "Critical" },
            new CountryFraudStats { Country = "Russia", CountryCode = "RU", FraudCount = 876, TotalAmount = 1987456.32m, RiskLevel = "High" },
            new CountryFraudStats { Country = "United States", CountryCode = "US", FraudCount = 1523, TotalAmount = 4567891.23m, RiskLevel = "High" },
            new CountryFraudStats { Country = "United Kingdom", CountryCode = "GB", FraudCount = 745, TotalAmount = 1876543.21m, RiskLevel = "High" },
            new CountryFraudStats { Country = "UAE", CountryCode = "AE", FraudCount = 634, TotalAmount = 2134567.89m, RiskLevel = "Medium" },
            new CountryFraudStats { Country = "India", CountryCode = "IN", FraudCount = 892, TotalAmount = 987654.32m, RiskLevel = "Medium" },
            new CountryFraudStats { Country = "Brazil", CountryCode = "BR", FraudCount = 567, TotalAmount = 1234567.89m, RiskLevel = "Medium" },
            new CountryFraudStats { Country = "Netherlands", CountryCode = "NL", FraudCount = 423, TotalAmount = 876543.21m, RiskLevel = "Medium" },
            new CountryFraudStats { Country = "Singapore", CountryCode = "SG", FraudCount = 398, TotalAmount = 1543210.98m, RiskLevel = "Medium" }
        };
    }
}

public class FraudHeatmapPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Intensity { get; set; }
    public double FraudScore { get; set; }
    public double Amount { get; set; }
    public string Country { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class CountryFraudStats
{
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public int FraudCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
}
