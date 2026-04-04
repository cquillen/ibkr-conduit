using System.Net;
using System.Net.Http;
using Refit;

namespace IbkrConduit.Tests.Unit.TestHelpers;

/// <summary>
/// Helper for creating fake <see cref="IApiResponse{T}"/> instances in unit tests.
/// </summary>
internal static class FakeApiResponse
{
    /// <summary>
    /// Creates a successful <see cref="IApiResponse{T}"/> wrapping the given content.
    /// </summary>
    public static IApiResponse<T> Success<T>(T content) =>
        new ApiResponse<T>(
            new HttpResponseMessage(HttpStatusCode.OK),
            content,
            new RefitSettings());

    /// <summary>
    /// Creates a failed <see cref="IApiResponse{T}"/> with the given status code.
    /// </summary>
    public static IApiResponse<T> Failure<T>(HttpStatusCode statusCode, string? body = null) =>
        new ApiResponse<T>(
            new HttpResponseMessage(statusCode)
            {
                Content = body is not null ? new StringContent(body) : null,
            },
            default,
            new RefitSettings());
}
