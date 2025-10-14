using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace GlobalPaymentFraudDetection.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<IdempotencyMiddleware> _logger;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

    public IdempotencyMiddleware(
        RequestDelegate next, 
        IMemoryCache cache,
        ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsIdempotentRequest(context))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();

        if (string.IsNullOrEmpty(idempotencyKey))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"idempotency:{idempotencyKey}";

        if (_cache.TryGetValue<CachedResponse>(cacheKey, out var cachedResponse))
        {
            _logger.LogInformation("Idempotent request detected. Returning cached response for key: {IdempotencyKey}", idempotencyKey);
            
            context.Response.StatusCode = cachedResponse!.StatusCode;
            context.Response.ContentType = cachedResponse.ContentType;
            context.Response.Headers["X-Idempotency-Replay"] = "true";
            
            foreach (var header in cachedResponse.Headers)
            {
                if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
            }

            await context.Response.WriteAsync(cachedResponse.Body);
            return;
        }

        var processingKey = $"idempotency:processing:{idempotencyKey}";
        if (_cache.TryGetValue<bool>(processingKey, out _))
        {
            _logger.LogWarning("Concurrent duplicate request detected for key: {IdempotencyKey}", idempotencyKey);
            context.Response.StatusCode = 409;
            await context.Response.WriteAsync("{\"error\": \"Request is currently being processed\"}");
            return;
        }

        _cache.Set(processingKey, true, TimeSpan.FromMinutes(5));

        var originalBodyStream = context.Response.Body;

        try
        {
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                await _next(context);

                context.Response.Body.Seek(0, SeekOrigin.Begin);
                var responseBodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();
                context.Response.Body.Seek(0, SeekOrigin.Begin);

                var response = new CachedResponse
                {
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType ?? "application/json",
                    Body = responseBodyText,
                    Headers = context.Response.Headers
                        .Where(h => !h.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(h => h.Key, h => h.Value.ToString())
                };

                _cache.Set(cacheKey, response, _cacheExpiration);
                _logger.LogInformation("Cached response for idempotency key: {IdempotencyKey}", idempotencyKey);

                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
        finally
        {
            context.Response.Body = originalBodyStream;
            _cache.Remove(processingKey);
        }
    }

    private bool IsIdempotentRequest(HttpContext context)
    {
        return context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "PATCH";
    }

    private class CachedResponse
    {
        public int StatusCode { get; set; }
        public string ContentType { get; set; } = "application/json";
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
    }
}
