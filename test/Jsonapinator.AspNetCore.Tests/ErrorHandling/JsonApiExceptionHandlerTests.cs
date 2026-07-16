using Jsonapinator;
using Jsonapinator.AspNetCore;
using Jsonapinator.AspNetCore.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jsonapinator.AspNetCore.Tests.ErrorHandling;

public class JsonApiExceptionHandlerTests
{
    private sealed class CapturingLogger : ILogger<JsonApiExceptionHandler>
    {
        public Exception? LoggedException { get; private set; }
        public LogLevel? LoggedLevel { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LoggedLevel = logLevel;
            LoggedException = exception;
        }
    }

    private static DefaultHttpContext CreateHttpContext(string? accept)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        if (accept is not null)
        {
            context.Request.Headers.Accept = accept;
        }

        return context;
    }

    [Fact]
    public async Task TryHandleAsync_returns_true_and_writes_a_generic_json_api_error_when_negotiated()
    {
        var logger = new CapturingLogger();
        var handler = new JsonApiExceptionHandler(new JsonApiSerializer(), new JsonApiFormatterOptions(), logger);
        var httpContext = CreateHttpContext("application/vnd.api+json");
        var exception = new InvalidOperationException("some sensitive internal detail");

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, httpContext.Response.StatusCode);
        Assert.Equal("application/vnd.api+json", httpContext.Response.ContentType);

        httpContext.Response.Body.Position = 0;
        var body = new StreamReader(httpContext.Response.Body).ReadToEnd();
        Assert.Contains("An unexpected error occurred.", body);
        Assert.DoesNotContain("some sensitive internal detail", body);
    }

    [Fact]
    public async Task TryHandleAsync_returns_false_and_leaves_response_untouched_when_not_negotiated()
    {
        var logger = new CapturingLogger();
        var handler = new JsonApiExceptionHandler(new JsonApiSerializer(), new JsonApiFormatterOptions(), logger);
        var httpContext = CreateHttpContext(accept: null);

        var handled = await handler.TryHandleAsync(httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(200, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_logs_the_exception_in_both_handled_and_unhandled_cases()
    {
        var logger = new CapturingLogger();
        var handler = new JsonApiExceptionHandler(new JsonApiSerializer(), new JsonApiFormatterOptions(), logger);
        var exception = new InvalidOperationException("boom");

        await handler.TryHandleAsync(CreateHttpContext(accept: null), exception, CancellationToken.None);

        Assert.Equal(LogLevel.Error, logger.LoggedLevel);
        Assert.Same(exception, logger.LoggedException);
    }
}
