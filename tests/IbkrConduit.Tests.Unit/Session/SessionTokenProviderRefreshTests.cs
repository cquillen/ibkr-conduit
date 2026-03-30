using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Session;

public class SessionTokenProviderRefreshTests
{
    [Fact]
    public async Task RefreshAsync_ReplacesExistingCachedToken()
    {
        var originalToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var refreshedToken = new LiveSessionToken(
            new byte[] { 0x04, 0x05, 0x06 },
            DateTimeOffset.UtcNow.AddHours(48));

        var client = new SequentialFakeLstClient(originalToken, refreshedToken);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        // Acquire first token
        var first = await provider.GetLiveSessionTokenAsync(CancellationToken.None);
        first.ShouldBe(originalToken);

        // Refresh should acquire a new token
        var refreshed = await provider.RefreshAsync(CancellationToken.None);
        refreshed.ShouldBe(refreshedToken);
        client.CallCount.ShouldBe(2);

        // Subsequent GetLiveSessionTokenAsync should return the refreshed token
        var cached = await provider.GetLiveSessionTokenAsync(CancellationToken.None);
        cached.ShouldBe(refreshedToken);
        client.CallCount.ShouldBe(2); // no additional call
    }

    [Fact]
    public async Task RefreshAsync_WithoutPriorGet_AcquiresNewToken()
    {
        var token = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var client = new SequentialFakeLstClient(token);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        var result = await provider.RefreshAsync(CancellationToken.None);

        result.ShouldBe(token);
        client.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshAsync_ConcurrentCalls_OnlyRefreshesOnce()
    {
        var originalToken = new LiveSessionToken(
            new byte[] { 0x01, 0x02, 0x03 },
            DateTimeOffset.UtcNow.AddHours(24));

        var refreshedToken = new LiveSessionToken(
            new byte[] { 0x04, 0x05, 0x06 },
            DateTimeOffset.UtcNow.AddHours(48));

        var client = new SequentialFakeLstClient(originalToken, refreshedToken);
        client.Delay = TimeSpan.FromMilliseconds(50);
        var creds = CreateTestCredentials();
        var provider = new SessionTokenProvider(creds, client);

        // Prime the cache
        await provider.GetLiveSessionTokenAsync(CancellationToken.None);

        // Fire multiple concurrent refreshes
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => provider.RefreshAsync(CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // All should get the same refreshed token
        foreach (var result in results)
        {
            result.ShouldBe(refreshedToken);
        }

        // Only 2 calls total: 1 initial + 1 refresh (not 10 refreshes)
        client.CallCount.ShouldBe(2);
    }

    private static IbkrOAuthCredentials CreateTestCredentials()
    {
        var sigKey = System.Security.Cryptography.RSA.Create(2048);
        var encKey = System.Security.Cryptography.RSA.Create(2048);
        return new IbkrOAuthCredentials(
            "tenant1", "TESTKEY01", "token", "secret",
            sigKey, encKey, new System.Numerics.BigInteger(23));
    }

    private class SequentialFakeLstClient : ILiveSessionTokenClient
    {
        private readonly LiveSessionToken[] _tokens;
        private int _index;

        public SequentialFakeLstClient(params LiveSessionToken[] tokens)
        {
            _tokens = tokens;
        }

        public int CallCount { get; private set; }
        public TimeSpan Delay { get; set; }

        public async Task<LiveSessionToken> GetLiveSessionTokenAsync(
            IbkrOAuthCredentials credentials, CancellationToken cancellationToken)
        {
            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            CallCount++;
            var token = _tokens[Math.Min(_index, _tokens.Length - 1)];
            _index++;
            return token;
        }
    }
}
