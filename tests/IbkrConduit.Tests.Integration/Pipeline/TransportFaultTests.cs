using System;
using System.Net.Http;
using System.Threading.Tasks;
using Shouldly;

namespace IbkrConduit.Tests.Integration.Pipeline;

/// <summary>
/// Integration tests verifying that a pre-response transport failure on a consumer
/// endpoint propagates as a thrown exception out of an operations call, rather than
/// being silently turned into a <c>Result.Failure</c>.
/// </summary>
public class TransportFaultTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task ConsumerCall_TransportFault_Throws()
    {
        // First call succeeds so the full session handshake (LST + ssodh/init) completes
        // and the live session token is cached. This isolates the fault to the consumer
        // Refit call itself, rather than the token-acquisition path in front of it.
        _harness.StubAuthenticatedGet("/v1/api/portfolio/accounts", "[]");
        var first = await _harness.Client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken);
        first.IsSuccess.ShouldBeTrue();

        // Shut the server down so the next consumer call hits a refused TCP connection — a
        // genuine pre-response transport fault that makes HttpClient.SendAsync throw, just
        // as a dropped/empty connection would. (WireMock.Net 2.7.0's WithFault does not
        // break the socket here; it returns a 200 with junk/empty content instead.)
        _harness.Server.Stop();

        // Refit 11 captures the SendAsync failure into ApiResponse.Error as an
        // ApiRequestException instead of letting it propagate. The ThrowOnSendFailure
        // guard wired into ResultFactory re-throws the original HttpRequestException so
        // the transport fault surfaces as a thrown exception rather than a silent
        // Result.Failure (Option A — preserve Refit 10 propagation semantics).
        await Should.ThrowAsync<HttpRequestException>(
            () => _harness.Client.Portfolio.GetAccountsAsync(TestContext.Current.CancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
