using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class SessionTokenProviderTests
{
    [Fact]
    public async Task GetLiveSessionTokenAsync_CachedTokenExpired_ReacquiresFreshToken()
    {
        // SES-3: GetLiveSessionTokenAsync must treat an expired cached token as a miss and
        // re-acquire — otherwise an expired LST is replayed forever, signing ssodh/init calls
        // that 401 and wedge the tenant even after IBKR is healthy again.
        var fakeTime = new FakeTimeProvider();
        var t0 = fakeTime.GetUtcNow();
        var expiredToken = new LiveSessionToken(new byte[] { 0x01 }, t0.AddHours(1));
        var freshToken = new LiveSessionToken(new byte[] { 0x02 }, t0.AddHours(25));
        var client = new SequenceLstClient(expiredToken, freshToken);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client, fakeTime);

        var first = await provider.GetLiveSessionTokenAsync(CancellationToken.None);
        first.ShouldBe(expiredToken);
        client.CallCount.ShouldBe(1);

        // Advance past the first token's expiry.
        fakeTime.Advance(TimeSpan.FromHours(2));

        var second = await provider.GetLiveSessionTokenAsync(CancellationToken.None);

        second.ShouldBe(freshToken);
        client.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetLiveSessionTokenAsync_CachedTokenNotExpired_ReturnsCachedWithoutReacquiring()
    {
        var fakeTime = new FakeTimeProvider();
        var t0 = fakeTime.GetUtcNow();
        var token = new LiveSessionToken(new byte[] { 0x01 }, t0.AddHours(24));
        var client = new SequenceLstClient(token);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client, fakeTime);

        var first = await provider.GetLiveSessionTokenAsync(CancellationToken.None);
        fakeTime.Advance(TimeSpan.FromHours(1)); // still well before expiry
        var second = await provider.GetLiveSessionTokenAsync(CancellationToken.None);

        first.ShouldBe(token);
        second.ShouldBe(token);
        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetLiveSessionTokenAsync_FirstCall_AcquiresFromClient()
    {
        var expectedToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var client = new FakeLstClient(expectedToken);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        var result = await provider.GetLiveSessionTokenAsync(CancellationToken.None);

        result.ShouldBe(expectedToken);
        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetLiveSessionTokenAsync_SecondCall_ReturnsCached()
    {
        var expectedToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var client = new FakeLstClient(expectedToken);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        var result1 = await provider.GetLiveSessionTokenAsync(CancellationToken.None);
        var result2 = await provider.GetLiveSessionTokenAsync(CancellationToken.None);

        result1.ShouldBe(result2);
        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetLiveSessionTokenAsync_ConcurrentCalls_OnlyAcquiresOnce()
    {
        var expectedToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var client = new FakeLstClient(expectedToken, delay: TimeSpan.FromMilliseconds(50));
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => provider.GetLiveSessionTokenAsync(CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            result.ShouldBe(expectedToken);
        }

        client.CallCount.ShouldBe(1);
    }

    private static IbkrOAuthCredentials CreateTestCredentials()
    {
        var sigKey = System.Security.Cryptography.RSA.Create(2048);
        var encKey = System.Security.Cryptography.RSA.Create(2048);
        return new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "token", "secret",
            sigKey, encKey, new System.Numerics.BigInteger(23));
    }

    /// <summary>
    /// Returns a preset sequence of tokens, one per call, so a test can assert re-acquisition
    /// (each fresh acquisition yields a distinct token). Once the sequence is exhausted the last
    /// token is repeated.
    /// </summary>
    private sealed class SequenceLstClient : ILiveSessionTokenClient
    {
        private readonly Queue<LiveSessionToken> _tokens;
        private LiveSessionToken _last;

        public SequenceLstClient(params LiveSessionToken[] tokens)
        {
            _tokens = new Queue<LiveSessionToken>(tokens);
            _last = tokens[^1];
        }

        public int CallCount { get; private set; }

        public Task<LiveSessionToken> GetLiveSessionTokenAsync(
            IbkrOAuthCredentials credentials, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_tokens.Count > 0)
            {
                _last = _tokens.Dequeue();
            }

            return Task.FromResult(_last);
        }
    }

    private class FakeLstClient : ILiveSessionTokenClient
    {
        private readonly LiveSessionToken _token;
        private readonly TimeSpan _delay;

        public FakeLstClient(LiveSessionToken token, TimeSpan delay = default)
        {
            _token = token;
            _delay = delay;
        }

        public int CallCount { get; private set; }

        public async Task<LiveSessionToken> GetLiveSessionTokenAsync(
            IbkrOAuthCredentials credentials, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, cancellationToken);
            }

            return _token;
        }
    }
}
