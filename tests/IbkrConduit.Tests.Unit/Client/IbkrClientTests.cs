using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Orders;
using IbkrConduit.Portfolio;
using IbkrConduit.Session;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Client;

public class IbkrClientTests
{
    [Fact]
    public void Portfolio_ReturnsSameInstance()
    {
        var portfolio = new FakePortfolioOperations();
        var client = CreateClient(portfolio: portfolio);

        client.Portfolio.ShouldBeSameAs(portfolio);
    }

    [Fact]
    public void Contracts_ReturnsSameInstance()
    {
        var contracts = new FakeContractOperations();
        var client = CreateClient(contracts: contracts);

        client.Contracts.ShouldBeSameAs(contracts);
    }

    [Fact]
    public void Orders_ReturnsSameInstance()
    {
        var orders = new FakeOrderOperations();
        var client = CreateClient(orders: orders);

        client.Orders.ShouldBeSameAs(orders);
    }

    [Fact]
    public async Task DisposeAsync_DisposesSessionManager()
    {
        var sessionManager = new FakeSessionManager();
        var client = CreateClient(sessionManager: sessionManager);

        await client.DisposeAsync();

        sessionManager.Disposed.ShouldBeTrue();
    }

    private static IbkrClient CreateClient(
        IPortfolioOperations? portfolio = null,
        IContractOperations? contracts = null,
        IOrderOperations? orders = null,
        ISessionManager? sessionManager = null) =>
        new(
            portfolio ?? new FakePortfolioOperations(),
            contracts ?? new FakeContractOperations(),
            orders ?? new FakeOrderOperations(),
            sessionManager ?? new FakeSessionManager());

    private class FakePortfolioOperations : IPortfolioOperations
    {
        public Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Account>());
    }

    private class FakeContractOperations : IContractOperations
    {
        public Task<List<ContractSearchResult>> SearchBySymbolAsync(
            string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ContractSearchResult>());

        public Task<ContractDetails> GetContractDetailsAsync(
            string conid, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private class FakeOrderOperations : IOrderOperations
    {
        public Task<OrderResult> PlaceOrderAsync(
            string accountId, OrderRequest order, CancellationToken cancellationToken = default) =>
            Task.FromResult(new OrderResult("1", "Submitted"));

        public Task<CancelOrderResponse> CancelOrderAsync(
            string accountId, string orderId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<LiveOrder>> GetLiveOrdersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<LiveOrder>());

        public Task<List<Trade>> GetTradesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Trade>());
    }

    private class FakeSessionManager : ISessionManager
    {
        public bool Disposed { get; private set; }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReauthenticateAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
