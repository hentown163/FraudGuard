using System.Diagnostics;

namespace GlobalPaymentFraudDetection.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();
        
        context.Items["RequestId"] = requestId;

        _logger.LogInformation(
            "Incoming Request: {Method} {Path} | RequestId: {RequestId} | IP: {IpAddress}",
            context.Request.Method,
            context.Request.Path,
            requestId,
            context.Connection.RemoteIpAddress?.ToString()
        );

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Completed Request: {Method} {Path} | RequestId: {RequestId} | Status: {StatusCode} | Duration: {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                requestId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds
            );
        }
    }
}
