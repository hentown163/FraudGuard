using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using GlobalPaymentFraudDetection.Middleware;
using System.Text.Json;

namespace GlobalPaymentFraudDetection.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;

    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _environmentMock.Setup(x => x.EnvironmentName).Returns("Development");
    }

    [Fact]
    public async Task InvokeAsync_WithNoException_CallsNextMiddleware()
    {
        var nextCalled = false;
        RequestDelegate next = (HttpContext hc) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithArgumentNullException_ReturnsBadRequest()
    {
        RequestDelegate next = (HttpContext hc) => throw new ArgumentNullException("test");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthorizedAccessException_ReturnsUnauthorized()
    {
        RequestDelegate next = (HttpContext hc) => throw new UnauthorizedAccessException();

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_WithKeyNotFoundException_ReturnsNotFound()
    {
        RequestDelegate next = (HttpContext hc) => throw new KeyNotFoundException();

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_WithGenericException_ReturnsInternalServerError()
    {
        RequestDelegate next = (HttpContext hc) => throw new Exception("Test exception");

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_LogsException()
    {
        var exception = new Exception("Test exception");
        RequestDelegate next = (HttpContext hc) => throw exception;

        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object, _environmentMock.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
