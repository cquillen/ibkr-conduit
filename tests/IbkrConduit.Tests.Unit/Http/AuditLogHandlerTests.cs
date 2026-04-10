using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using IbkrConduit.Http;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class AuditLogHandlerTests
{
    [Fact]
    public async Task SendAsync_LogsRequestAndResponseAtDebugLevel()
    {
        var logger = new CapturingLogger();
        var responseBody = """{"accounts":["U1234567"]}""";
        var handler = new AuditLogHandler(logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, responseBody),
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };
        var response = await client.GetAsync("/v1/api/iserver/accounts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify request was logged
        logger.Messages.ShouldContain(m => m.Contains("→ GET /v1/api/iserver/accounts"));

        // Verify response was logged with status code and body
        logger.Messages.ShouldContain(m => m.Contains("← /v1/api/iserver/accounts 200") && m.Contains(responseBody));
    }

    [Fact]
    public async Task SendAsync_LogsRequestBody()
    {
        var logger = new CapturingLogger();
        var handler = new AuditLogHandler(logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, "{}"),
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };
        var requestBody = """{"orders":[{"conid":756733,"side":"BUY"}]}""";
        var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        await client.PostAsync("/v1/api/iserver/account/U1234567/orders", content, TestContext.Current.CancellationToken);

        logger.Messages.ShouldContain(m => m.Contains("→ POST") && m.Contains(requestBody));
    }

    [Fact]
    public async Task SendAsync_LogsErrorResponse()
    {
        var logger = new CapturingLogger();
        var errorBody = """{"error":"insufficient funds"}""";
        var handler = new AuditLogHandler(logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.BadRequest, errorBody),
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };
        var response = await client.GetAsync("/v1/api/test", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        logger.Messages.ShouldContain(m => m.Contains("← /v1/api/test 400") && m.Contains(errorBody));
    }

    [Fact]
    public async Task SendAsync_TruncatesLargeResponseBody()
    {
        var logger = new CapturingLogger();
        var largeBody = new string('x', 8000);
        var handler = new AuditLogHandler(logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, largeBody),
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };
        await client.GetAsync("/v1/api/test", TestContext.Current.CancellationToken);

        // Body should be truncated to 4KB + "[truncated]"
        var responseLog = logger.Messages.First(m => m.Contains("← /v1/api/test"));
        responseLog.ShouldContain("[truncated]");
        responseLog.ShouldNotContain(largeBody); // full body should NOT appear
    }

    [Fact]
    public async Task SendAsync_PreservesResponseBodyForDownstreamConsumers()
    {
        var logger = new CapturingLogger();
        var responseBody = """{"key":"value"}""";
        var handler = new AuditLogHandler(logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, responseBody),
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };
        var response = await client.GetAsync("/v1/api/test", TestContext.Current.CancellationToken);

        // The body should still be readable after the handler logged it
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe(responseBody);
    }

    [Fact]
    public async Task SendAsync_LogsDurationMs()
    {
        var logger = new CapturingLogger();
        var handler = new AuditLogHandler(logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, "{}"),
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };
        await client.GetAsync("/v1/api/test", TestContext.Current.CancellationToken);

        // Response log should include "ms)"
        logger.Messages.ShouldContain(m => m.Contains("ms)"));
    }

    [Fact]
    public async Task SendAsync_NoLoggingWhenDebugDisabled()
    {
        var logger = new CapturingLogger(enableDebug: false);
        var handler = new AuditLogHandler(logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, """{"data":true}"""),
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.ibkr.com") };
        await client.GetAsync("/v1/api/test", TestContext.Current.CancellationToken);

        // No log messages should be captured when Debug is disabled
        logger.Messages.ShouldBeEmpty();
    }

    [Fact]
    public void SanitizeUrl_FlexSendRequest_MasksToken()
    {
        var uri = new Uri("https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/SendRequest?t=SECRET_TOKEN_123&q=1464458&v=3");

        var result = AuditLogHandler.SanitizeUrl(uri);

        result.ShouldContain("t=***");
        result.ShouldNotContain("SECRET_TOKEN_123");
        result.ShouldContain("q=1464458");
        result.ShouldContain("v=3");
    }

    [Fact]
    public void SanitizeUrl_FlexGetStatement_MasksToken()
    {
        var uri = new Uri("https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/GetStatement?t=MY_FLEX_TOKEN&q=REF123&v=3");

        var result = AuditLogHandler.SanitizeUrl(uri);

        result.ShouldContain("t=***");
        result.ShouldNotContain("MY_FLEX_TOKEN");
        result.ShouldContain("q=REF123");
    }

    [Fact]
    public void SanitizeUrl_NonFlexUrl_PreservesFullPathAndQuery()
    {
        var uri = new Uri("https://api.ibkr.com/v1/api/iserver/accounts?filter=active");

        var result = AuditLogHandler.SanitizeUrl(uri);

        result.ShouldBe("/v1/api/iserver/accounts?filter=active");
    }

    [Fact]
    public void SanitizeUrl_NonFlexUrlWithTokenParam_DoesNotMask()
    {
        // Only Flex URLs get masked — a 't=' parameter on a non-Flex URL should be preserved
        var uri = new Uri("https://api.ibkr.com/v1/api/something?t=not_a_flex_token&other=value");

        var result = AuditLogHandler.SanitizeUrl(uri);

        result.ShouldContain("t=not_a_flex_token");
    }

    [Fact]
    public void SanitizeUrl_NullUri_ReturnsUnknown()
    {
        AuditLogHandler.SanitizeUrl(null).ShouldBe("unknown");
    }

    [Fact]
    public async Task SendAsync_FlexUrl_LogsMaskedToken()
    {
        var logger = new CapturingLogger();
        var handler = new AuditLogHandler(logger)
        {
            InnerHandler = new StubHandler(HttpStatusCode.OK, "<FlexStatementResponse />"),
        };

        using var client = new HttpClient(handler);
        await client.GetAsync(
            "https://ndcdyn.interactivebrokers.com/AccountManagement/FlexWebService/SendRequest?t=SECRET&q=123&v=3",
            TestContext.Current.CancellationToken);

        // Request log should have masked token
        logger.Messages.ShouldContain(m => m.Contains("t=***") && !m.Contains("SECRET"));
        // Response log should also have masked token
        logger.Messages.ShouldContain(m => m.Contains("← ") && m.Contains("t=***"));
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    /// <summary>
    /// Simple logger that captures formatted messages for assertion.
    /// </summary>
    private sealed class CapturingLogger(bool enableDebug = true) : ILogger<AuditLogHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }

        public bool IsEnabled(LogLevel logLevel) => enableDebug && logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
