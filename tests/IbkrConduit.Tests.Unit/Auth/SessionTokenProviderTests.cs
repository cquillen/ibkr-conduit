using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Auth;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Auth;

public class SessionTokenProviderTests
{
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
