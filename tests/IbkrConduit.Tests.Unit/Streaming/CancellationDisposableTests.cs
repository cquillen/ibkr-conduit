using System;
using System.Threading;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class CancellationDisposableTests
{
    [Fact]
    public void Dispose_CancelsAndDisposesTokenSource()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var disposable = new CancellationDisposable(cts);

        disposable.Dispose();

        token.IsCancellationRequested.ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => _ = cts.Token);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var cts = new CancellationTokenSource();
        var disposable = new CancellationDisposable(cts);

        disposable.Dispose();
        disposable.Dispose();
    }
}
