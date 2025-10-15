using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Middleware;

namespace GlobalPaymentFraudDetection.Tests.Middleware;

public class RequestLoggingMiddlewareTests
{
    private readonly Mock<ILogger<RequestLoggingMiddleware>> _loggerMock;

    public RequestLoggingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<RequestLoggingMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_LogsIncomingRequest()
    {
        RequestDelegate next = (HttpContext hc) => Task.CompletedTask;
        var middleware = new RequestLoggingMiddleware(next, _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Incoming Request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_LogsCompletedRequest()
    {
        RequestDelegate next = (HttpContext hc) =>
        {
            hc.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, _loggerMock.Object);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/test";

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Completed Request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_AddsRequestIdToContext()
    {
        RequestDelegate next = (HttpContext hc) => Task.CompletedTask;
        var middleware = new RequestLoggingMiddleware(next, _loggerMock.Object);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Items.Should().ContainKey("RequestId");
        context.Items["RequestId"].Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_LogsRequestDuration()
    {
        RequestDelegate next = async (HttpContext hc) =>
        {
            await Task.Delay(50);
        };

        var middleware = new RequestLoggingMiddleware(next, _loggerMock.Object);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Duration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
