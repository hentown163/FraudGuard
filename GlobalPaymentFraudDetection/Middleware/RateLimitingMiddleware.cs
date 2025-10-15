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

        var rateLimitEntry = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = _timeWindow;
            return new RateLimitEntry
            {
                Lock = new SemaphoreSlim(1, 1),
                Counter = new RequestCounter
                {
                    Count = 0,
                    WindowStart = DateTime.UtcNow
                }
            };
        })!;

        await rateLimitEntry.Lock.WaitAsync();
        try
        {
            if (DateTime.UtcNow - rateLimitEntry.Counter.WindowStart > _timeWindow)
            {
                rateLimitEntry.Counter.Count = 0;
                rateLimitEntry.Counter.WindowStart = DateTime.UtcNow;
            }

            rateLimitEntry.Counter.Count++;

            if (rateLimitEntry.Counter.Count > _requestLimit)
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

            context.Response.Headers.Append("X-Rate-Limit-Remaining", (_requestLimit - rateLimitEntry.Counter.Count).ToString());
        }
        finally
        {
            rateLimitEntry.Lock.Release();
        }

        await _next(context);
    }

    private class RateLimitEntry
    {
        public required SemaphoreSlim Lock { get; init; }
        public required RequestCounter Counter { get; init; }
    }

    private class RequestCounter
    {
        public int Count { get; set; }
        public DateTime WindowStart { get; set; }
    }
}
