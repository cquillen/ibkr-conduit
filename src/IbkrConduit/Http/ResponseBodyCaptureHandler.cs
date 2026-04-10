using System.Text;

namespace IbkrConduit.Http;

/// <summary>
/// DelegatingHandler that captures the raw response body and stashes it in
/// <see cref="HttpRequestMessage.Options"/> for downstream consumption by
/// <see cref="IbkrConduit.Errors.ResultFactory"/>. This enables hidden error
/// detection on 200 OK responses where Refit consumes the body during deserialization,
/// leaving <c>response.Error?.Content</c> empty.
/// </summary>
internal sealed class ResponseBodyCaptureHandler : DelegatingHandler
{
    /// <summary>
    /// The key used to store the captured raw body in <see cref="HttpRequestMessage.Options"/>.
    /// </summary>
    internal const string RawBodyOptionKey = "IbkrConduit.RawResponseBody";

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.Content is not null)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // Stash the raw body for ResultFactory to read later
            request.Options.Set(new HttpRequestOptionsKey<string>(RawBodyOptionKey), body);

            // Re-buffer the body so Refit can still deserialize it
            var contentType = response.Content.Headers.ContentType;
            response.Content = new StringContent(body, Encoding.UTF8);
            if (contentType is not null)
            {
                response.Content.Headers.ContentType = contentType;
            }
        }

        return response;
    }
}
