using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Refit;

namespace IbkrConduit.Tests.Unit.Http;

[ExcludeFromCodeCoverage]
public record TestDto([property: JsonPropertyName("id")] string Id);

[ExcludeFromCodeCoverage]
public record TestItem([property: JsonPropertyName("name")] string Name);

[ExcludeFromCodeCoverage]
public record TestValue([property: JsonPropertyName("amount")] decimal Amount);

public interface ITestApi
{
    [Get("/v1/api/test/{id}")]
    Task<TestDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    [Get("/v1/api/test/items")]
    Task<List<TestItem>> GetItemsAsync(CancellationToken cancellationToken = default);

    [Get("/v1/api/test/{accountId}/values")]
    Task<Dictionary<string, TestValue>> GetValuesAsync(
        string accountId, CancellationToken cancellationToken = default);

    [Post("/v1/api/test/action")]
    Task DoActionAsync(CancellationToken cancellationToken = default);

    [Delete("/v1/api/test/{id}")]
    Task<TestDto> DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface ITestApiWithRawResponse
{
    [Post("/v1/api/test/raw")]
    Task<IApiResponse<string>> GetRawAsync(CancellationToken cancellationToken = default);
}
