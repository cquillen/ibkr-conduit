using System;
using IbkrConduit.Client;
using IbkrConduit.Health;

namespace IbkrConduit.Tests.Unit.Client;

// Minimal IIbkrClient whose members the manager tests never call. Every member
// throws NotSupportedException so accidental use is caught. Only identity is asserted.
internal sealed class StubIbkrClient : IIbkrClient
{
    public IPortfolioOperations Portfolio => throw new NotSupportedException();
    public IContractOperations Contracts => throw new NotSupportedException();
    public IOrderOperations Orders => throw new NotSupportedException();
    public IMarketDataOperations MarketData => throw new NotSupportedException();
    public IStreamingOperations Streaming => throw new NotSupportedException();
    public IFlexOperations Flex => throw new NotSupportedException();
    public IAccountOperations Accounts => throw new NotSupportedException();
    public IAlertOperations Alerts => throw new NotSupportedException();
    public IWatchlistOperations Watchlists => throw new NotSupportedException();
    public IFyiOperations Notifications => throw new NotSupportedException();
    public IEventContractOperations EventContracts => throw new NotSupportedException();

    public Task<IbkrHealthStatus> GetHealthStatusAsync(
        bool activeProbe = false, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task ValidateConnectionAsync(bool validateFlex = true, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask DisposeAsync() => throw new NotSupportedException();
}
