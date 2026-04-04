using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Errors;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class ResponseSchemaValidationHandlerTests
{
    // --- Test DTOs ---

    [ExcludeFromCodeCoverage]
    public record TestDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    [ExcludeFromCodeCoverage]
    public record TestDtoWithOptional(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("optional_field")] string? OptionalField);

    [ExcludeFromCodeCoverage]
    public record TestDtoWithExtension(
        [property: JsonPropertyName("id")] string Id)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; init; }
    }

    // --- Test Refit interface ---

    public interface ITestValidationApi
    {
        [Refit.Get("/v1/api/test/item")]
        Task<TestDto> GetItemAsync(CancellationToken cancellationToken = default);

        [Refit.Get("/v1/api/test/optional")]
        Task<TestDtoWithOptional> GetOptionalAsync(CancellationToken cancellationToken = default);

        [Refit.Get("/v1/api/test/extension")]
        Task<TestDtoWithExtension> GetExtensionAsync(CancellationToken cancellationToken = default);

        [Refit.Get("/v1/api/test/items")]
        Task<List<TestDto>> GetItemsAsync(CancellationToken cancellationToken = default);
    }

    // --- Helpers ---

    private static RefitEndpointMap BuildMap() =>
        RefitEndpointMap.Build([typeof(ITestValidationApi)]);

    private static ResponseSchemaValidationHandler CreateHandler(
        bool strict, RefitEndpointMap map, HttpResponseMessage response)
    {
        var options = new IbkrClientOptions { StrictResponseValidation = strict };
        var logger = NullLoggerFactory.Instance.CreateLogger<ResponseSchemaValidationHandler>();
        var handler = new ResponseSchemaValidationHandler(options, map, logger)
        {
            InnerHandler = new StubInnerHandler(response),
        };
        return handler;
    }

    private static HttpRequestMessage MakeRequest(HttpMethod method, string path) =>
        new(method, $"https://api.ibkr.com{path}");

    private static HttpResponseMessage MakeJsonResponse(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Task<HttpResponseMessage> SendAsync(
        ResponseSchemaValidationHandler handler, HttpRequestMessage request) =>
        new HttpMessageInvoker(handler).SendAsync(request, TestContext.Current.CancellationToken);

    // --- Tests ---

    [Fact]
    public async Task StrictMode_ExtraField_ThrowsSchemaViolationException()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","name":"test","unexpected":"value"}""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item")));

        ex.ExtraFields.ShouldContain("unexpected");
        ex.DtoType.ShouldBe(typeof(TestDto));
        ex.EndpointPath.ShouldBe("/v1/api/test/item");
    }

    [Fact]
    public async Task StrictMode_MissingRequiredField_ThrowsSchemaViolationException()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1"}""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item")));

        ex.MissingFields.ShouldContain("name");
    }

    [Fact]
    public async Task StrictMode_MissingOptionalField_DoesNotThrow()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/optional"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StrictMode_ExtensionData_ExtraFieldsNotFlagged()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","extra_field":"value","another":"value2"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/extension"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonStrictMode_ExtraField_DoesNotThrow()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","name":"test","unexpected":"value"}""");
        var handler = CreateHandler(strict: false, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonSuccessResponse_SkipsValidation()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"error":"not found"}""", HttpStatusCode.NotFound);
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnknownEndpoint_SkipsValidation()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"random":"data"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/unknown/path"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StrictMode_MatchingFields_PassesThrough()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","name":"test"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BodyPreservedAfterValidation()
    {
        var map = BuildMap();
        var body = """{"id":"1","name":"test"}""";
        var response = MakeJsonResponse(body);
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        var content = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.ShouldBe(body);
    }

    [Fact]
    public async Task ContentTypePreservedAfterValidation()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","name":"test"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task ListResponse_ValidatesFirstElement()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""[{"id":"1","name":"test","extra":"field"}]""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/items")));

        ex.ExtraFields.ShouldContain("extra");
    }

    [Fact]
    public async Task EmptyArrayResponse_SkipsValidation()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("[]");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/items"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonJsonResponse_SkipsValidation()
    {
        var map = BuildMap();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("plain text", Encoding.UTF8, "text/plain"),
        };
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EmptyBody_SkipsValidation()
    {
        var map = BuildMap();
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("", Encoding.UTF8, "application/json"),
        };
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/item"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private class StubInnerHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
