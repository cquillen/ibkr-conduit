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

    // A row element inside a wrapper DTO — both fields required so drift is observable.
    [ExcludeFromCodeCoverage]
    public record TestRow(
        [property: JsonPropertyName("row_id")] string RowId,
        [property: JsonPropertyName("row_name")] string RowName);

    // A wrapper DTO holding a List<T> of rows plus a scalar priming flag — mirrors OrdersResponse.
    [ExcludeFromCodeCoverage]
    public record TestWrapper(
        [property: JsonPropertyName("rows")] List<TestRow>? Rows,
        [property: JsonPropertyName("snapshot")] bool? Snapshot = null);

    // A nested (non-collection) child DTO reached via a scalar object property.
    [ExcludeFromCodeCoverage]
    public record TestNestedChild(
        [property: JsonPropertyName("child_id")] string ChildId);

    [ExcludeFromCodeCoverage]
    public record TestParent(
        [property: JsonPropertyName("parent_id")] string ParentId,
        [property: JsonPropertyName("child")] TestNestedChild Child);

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

        [Refit.Get("/v1/api/test/wrapper")]
        Task<TestWrapper> GetWrapperAsync(CancellationToken cancellationToken = default);

        [Refit.Get("/v1/api/test/parent")]
        Task<TestParent> GetParentAsync(CancellationToken cancellationToken = default);

        [Refit.Post("/v1/api/test/reply/{id}")]
        Task<Refit.IApiResponse<string>> ReplyRawAsync(
            string id, CancellationToken cancellationToken = default);
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
    public async Task StrictMode_ExtensionData_ExtraFieldsFlagged()
    {
        // WIR-5: extra-field detection now runs even when the DTO has [JsonExtensionData]. A renamed
        // wire field lands silently in AdditionalData; surfacing it as an extra field makes the drift
        // observable instead of invisible (previously suppressed for all extension-data DTOs).
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","extra_field":"value","another":"value2"}""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/extension")));

        ex.ExtraFields.ShouldContain("extra_field");
        ex.ExtraFields.ShouldContain("another");
    }

    [Fact]
    public async Task NonStrictMode_ExtensionData_ExtraField_DoesNotThrow()
    {
        // In the default (non-strict) mode the newly-surfaced extra field on an extension-data DTO
        // is a Warning log only — default consumers are never broken by the WIR-5 tightening.
        var map = BuildMap();
        var response = MakeJsonResponse("""{"id":"1","extra_field":"value"}""");
        var handler = CreateHandler(strict: false, map, response);

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
    public async Task StrictMode_UnknownEndpoint_ThrowsSchemaViolationException()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"random":"data"}""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/unknown/path")));

        ex.MissingFields.ShouldContain(f => f.Contains("No DTO mapping"));
        ex.EndpointPath.ShouldBe("/v1/api/unknown/path");
    }

    [Fact]
    public async Task NonStrictMode_UnknownEndpoint_LogsErrorAndPassesThrough()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""{"random":"data"}""");
        var handler = CreateHandler(strict: false, map, response);

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
    public async Task StrictMode_MissingRequiredFieldOnSecondElement_ThrowsSchemaViolationException()
    {
        // WIR-5 (1): collection bodies are validated element-by-element, not just element[0] — a
        // required field vanished from a later element (e.g. price gone from trade #2) is now caught.
        var map = BuildMap();
        var response = MakeJsonResponse("""[{"id":"1","name":"a"},{"id":"2"}]""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/items")));

        ex.MissingFields.ShouldContain("name");
    }

    [Fact]
    public async Task StrictMode_ExtraFieldOnSecondElement_ThrowsSchemaViolationException()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""[{"id":"1","name":"a"},{"id":"2","name":"b","drifted":"x"}]""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/items")));

        ex.ExtraFields.ShouldContain("drifted");
    }

    [Fact]
    public async Task StrictMode_AllElementsWellFormed_PassesThrough()
    {
        var map = BuildMap();
        var response = MakeJsonResponse("""[{"id":"1","name":"a"},{"id":"2","name":"b"}]""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/items"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StrictMode_DriftOnNonFirstWrappedRow_ThrowsSchemaViolationException()
    {
        // WIR-2/TEN-2: a wrapper DTO's List<T> row property is now descended — a drifted/missing
        // field on a NON-first row element raises the signal (previously only the wrapper's
        // top-level fields were diffed, so row-level drift was invisible).
        var map = BuildMap();
        var response = MakeJsonResponse(
            """{"rows":[{"row_id":"1","row_name":"a"},{"row_id":"2","drifted":"x"}],"snapshot":true}""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/wrapper")));

        ex.ExtraFields.ShouldContain("drifted");
        ex.MissingFields.ShouldContain("row_name");
    }

    [Fact]
    public async Task StrictMode_AllWrappedRowsWellFormed_PassesThrough()
    {
        var map = BuildMap();
        var response = MakeJsonResponse(
            """{"rows":[{"row_id":"1","row_name":"a"},{"row_id":"2","row_name":"b"}],"snapshot":true}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/wrapper"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StrictMode_DriftOnNestedObject_ThrowsSchemaViolationException()
    {
        // WIR-2: nested object DTO maps were computed but never recursed — a drifted field on a
        // nested (non-collection) object is now surfaced.
        var map = BuildMap();
        var response = MakeJsonResponse(
            """{"parent_id":"1","child":{"child_id":"1","drifted_nested":"x"}}""");
        var handler = CreateHandler(strict: true, map, response);

        var ex = await Should.ThrowAsync<IbkrSchemaViolationException>(
            SendAsync(handler, MakeRequest(HttpMethod.Get, "/v1/api/test/parent")));

        ex.ExtraFields.ShouldContain("drifted_nested");
    }

    [Fact]
    public async Task StrictMode_KnownRawStringEndpoint_PassesThrough()
    {
        // WIR-2/TEN-2: string-returning endpoints resolve to a known-raw sentinel, so strict mode
        // skips them (their body is deliberately unvalidated) instead of treating them as an
        // unmapped violation — contrast StrictMode_UnknownEndpoint_* which still throws.
        var map = BuildMap();
        var response = MakeJsonResponse("""{"anything":"goes"}""");
        var handler = CreateHandler(strict: true, map, response);

        var result = await SendAsync(handler, MakeRequest(HttpMethod.Post, "/v1/api/test/reply/12345"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
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
