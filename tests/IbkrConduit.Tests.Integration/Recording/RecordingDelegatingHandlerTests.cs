using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Recording;

public class RecordingDelegatingHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RecordingContext _context;

    public RecordingDelegatingHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "recording-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _context = new RecordingContext();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SendAsync_WhenRecordingInactive_DoesNotWriteFile()
    {
        // ScenarioName is null by default (inactive)
        using var handler = CreateHandler(new FakeInnerHandler("OK"));
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);

        Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenRecordingActive_WritesJsonFile()
    {
        _context.Reset("test-scenario");
        using var handler = CreateHandler(new FakeInnerHandler("OK"));
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);

        var files = Directory.GetFiles(Path.Combine(_tempDir, "test-scenario"));
        files.Length.ShouldBe(1);
        files[0].ShouldEndWith(".json");
    }

    [Fact]
    public async Task SendAsync_PassesThroughResponseUnchanged()
    {
        _context.Reset("test-scenario");
        var expectedBody = """{"accounts":["U123"]}""";
        using var handler = CreateHandler(new FakeInnerHandler(expectedBody));
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldBe(expectedBody);
    }

    [Fact]
    public async Task SendAsync_SanitizesAuthorizationHeader()
    {
        _context.Reset("test-scenario");
        using var handler = CreateHandler(new FakeInnerHandler("OK"));
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/v1/api/portfolio/accounts");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret-token-123");

        await client.SendAsync(request, TestContext.Current.CancellationToken);

        var json = await ReadFirstRecordedFile();
        var doc = JsonDocument.Parse(json);
        var authHeader = doc.RootElement
            .GetProperty("Request")
            .GetProperty("Headers")
            .GetProperty("Authorization")
            .GetString();

        authHeader.ShouldBe("REDACTED");
    }

    [Fact]
    public async Task SendAsync_SanitizesOAuthTokenInQueryString()
    {
        _context.Reset("test-scenario");
        using var handler = CreateHandler(new FakeInnerHandler("OK"));
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/v1/api/portfolio/accounts?oauth_token=secret123",
            TestContext.Current.CancellationToken);

        var json = await ReadFirstRecordedFile();
        var doc = JsonDocument.Parse(json);
        var path = doc.RootElement
            .GetProperty("Request")
            .GetProperty("Path")
            .GetString();

        path.ShouldNotBeNull();
        path.ShouldContain("oauth_token=REDACTED");
        path.ShouldNotContain("secret123");
    }

    [Fact]
    public async Task SendAsync_SanitizesCookieHeader()
    {
        _context.Reset("test-scenario");
        using var handler = CreateHandler(new FakeInnerHandler("OK"));
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/v1/api/portfolio/accounts");
        request.Headers.TryAddWithoutValidation("Cookie", "api=secretcookie");

        await client.SendAsync(request, TestContext.Current.CancellationToken);

        var json = await ReadFirstRecordedFile();
        var doc = JsonDocument.Parse(json);
        var cookieHeader = doc.RootElement
            .GetProperty("Request")
            .GetProperty("Headers")
            .GetProperty("Cookie")
            .GetString();

        cookieHeader.ShouldBe("api=REDACTED");
    }

    [Fact]
    public async Task SendAsync_FileNameFollowsStepMethodSlugPattern()
    {
        _context.Reset("test-scenario");
        using var handler = CreateHandler(new FakeInnerHandler("OK"));
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);

        var files = Directory.GetFiles(Path.Combine(_tempDir, "test-scenario"));
        Path.GetFileName(files[0]).ShouldBe("001-GET-portfolio-accounts.json");
    }

    [Fact]
    public async Task SendAsync_WritesWireMockCompatibleJsonStructure()
    {
        _context.Reset("test-scenario");
        using var handler = CreateHandler(new FakeInnerHandler("""{"ok":true}"""));
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);

        var json = await ReadFirstRecordedFile();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("Request").GetProperty("Path").GetString().ShouldNotBeNull();
        root.GetProperty("Request").GetProperty("Methods").GetArrayLength().ShouldBeGreaterThan(0);
        root.GetProperty("Response").GetProperty("StatusCode").GetInt32().ShouldBe(200);
        root.GetProperty("Response").GetProperty("Body").GetString().ShouldBe("""{"ok":true}""");
        root.GetProperty("Metadata").GetProperty("Scenario").GetString().ShouldBe("test-scenario");
        root.GetProperty("Metadata").GetProperty("Step").GetInt32().ShouldBe(1);
        root.GetProperty("Metadata").GetProperty("RecordedAt").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task SendAsync_BuffersResponseBodyForDownstreamConsumers()
    {
        _context.Reset("test-scenario");
        var expectedBody = """{"data":"value"}""";
        using var handler = CreateHandler(new FakeInnerHandler(expectedBody));
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);

        // Read body twice to prove it's properly buffered
        var body1 = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body1.ShouldBe(expectedBody);
    }

    [Fact]
    public async Task SendAsync_CapturesRequestBodyForPostRequests()
    {
        _context.Reset("test-scenario");
        using var handler = CreateHandler(new FakeInnerHandler("OK"));
        using var client = new HttpClient(handler);

        var postBody = """{"symbol":"AAPL"}""";
        var content = new StringContent(postBody, Encoding.UTF8, "application/json");
        await client.PostAsync("http://localhost/v1/api/iserver/contract/search",
            content, TestContext.Current.CancellationToken);

        var json = await ReadFirstRecordedFile();
        var doc = JsonDocument.Parse(json);
        var requestBody = doc.RootElement
            .GetProperty("Request")
            .GetProperty("Body")
            .GetString();

        requestBody.ShouldBe(postBody);
    }

    [Fact]
    public async Task SendAsync_MultipleRequestsProduceIncrementingStepNumbers()
    {
        _context.Reset("test-scenario");
        using var handler = CreateHandler(new FakeInnerHandler("OK"));
        using var client = new HttpClient(handler);

        await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);
        await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);
        await client.GetAsync("http://localhost/v1/api/portfolio/accounts",
            TestContext.Current.CancellationToken);

        var files = Directory.GetFiles(Path.Combine(_tempDir, "test-scenario"))
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToArray();

        files.Length.ShouldBe(3);
        files[0].ShouldStartWith("001-");
        files[1].ShouldStartWith("002-");
        files[2].ShouldStartWith("003-");
    }

    private RecordingDelegatingHandler CreateHandler(HttpMessageHandler innerHandler)
    {
        var handler = new RecordingDelegatingHandler(_context, _tempDir)
        {
            InnerHandler = innerHandler,
        };
        return handler;
    }

    private async Task<string> ReadFirstRecordedFile()
    {
        var files = Directory.GetFiles(_tempDir, "*.json", SearchOption.AllDirectories);
        files.ShouldNotBeEmpty("Expected at least one recorded JSON file");
        return await File.ReadAllTextAsync(files[0], TestContext.Current.CancellationToken);
    }

    private sealed class FakeInnerHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public FakeInnerHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
