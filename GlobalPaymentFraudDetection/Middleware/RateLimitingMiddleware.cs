using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace GlobalPaymentFraudDetection.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly int _requestLimit = 100;
    private readonly TimeSpan _timeWindow = TimeSpan.FromMinutes(1);

    public RateLimitingMiddleware(
        RequestDelegate next, 
        IMemoryCache cache,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var endpoint = $"{context.Request.Method}:{context.Request.Path}";
        var cacheKey = $"rate_limit:{clientIp}:{endpoint}";

        if (!_cache.TryGetValue<RequestCounter>(cacheKey, out var counter))
        {
            counter = new RequestCounter
            {
                Count = 0,
                WindowStart = DateTime.UtcNow
            };
        }

        if (DateTime.UtcNow - counter!.WindowStart > _timeWindow)
        {
            counter.Count = 0;
            counter.WindowStart = DateTime.UtcNow;
        }

        counter.Count++;

        _cache.Set(cacheKey, counter, _timeWindow);

        if (counter.Count > _requestLimit)
        {
            _logger.LogWarning(
                "Rate limit exceeded for IP: {IpAddress} on endpoint: {Endpoint}",
                clientIp,
                endpoint
            );

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers.Append("Retry-After", _timeWindow.TotalSeconds.ToString());
            
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded. Please try again later.",
                retryAfter = _timeWindow.TotalSeconds
            });
            
            return;
        }

        context.Response.Headers.Append("X-Rate-Limit-Remaining", (_requestLimit - counter.Count).ToString());

        await _next(context);
    }

    private class RequestCounter
    {
        public int Count { get; set; }
        public DateTime WindowStart { get; set; }
    }
}
