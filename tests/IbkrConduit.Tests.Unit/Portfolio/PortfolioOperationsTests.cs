using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Portfolio;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Portfolio;

public class PortfolioOperationsTests
{
    private readonly FakePortfolioApi _fakeApi = new();
    private readonly PortfolioOperations _sut;

    public PortfolioOperationsTests()
    {
        _sut = new PortfolioOperations(_fakeApi);
    }

    [Fact]
    public async Task GetPositionsAsync_DelegatesToApi()
    {
        _fakeApi.PositionsResponse =
        [
            new Position("DU123", 265598, "SPY", 100m, 450.00m, 45000.00m,
                440.00m, 440.00m, 0m, 1000m, "USD", "SPDR S&P 500", "STK",
                null, "SPY", null, true),
        ];

        var result = await _sut.GetPositionsAsync("DU123", cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].AccountId.ShouldBe("DU123");
        result[0].Conid.ShouldBe(265598);
        result[0].Ticker.ShouldBe("SPY");
    }

    [Fact]
    public async Task GetAccountSummaryAsync_DelegatesToApi()
    {
        _fakeApi.SummaryResponse = new Dictionary<string, AccountSummaryEntry>
        {
            ["netliquidationvalue"] = new AccountSummaryEntry(100000.50m, "USD", false, 1702334859712, "100000.50"),
        };

        var result = await _sut.GetAccountSummaryAsync("DU123", TestContext.Current.CancellationToken);

        result.ShouldContainKey("netliquidationvalue");
        result["netliquidationvalue"].Amount.ShouldBe(100000.50m);
    }

    [Fact]
    public async Task GetLedgerAsync_DelegatesToApi()
    {
        _fakeApi.LedgerResponse = new Dictionary<string, LedgerEntry>
        {
            ["USD"] = new LedgerEntry(50000m, 100000m, 48000m, 1.0m, 50000m, 0m, 0m, 0m, 0m),
        };

        var result = await _sut.GetLedgerAsync("DU123", TestContext.Current.CancellationToken);

        result.ShouldContainKey("USD");
        result["USD"].CashBalance.ShouldBe(50000m);
    }

    [Fact]
    public async Task GetAccountPerformanceAsync_ConstructsRequestAndDelegates()
    {
        _fakeApi.PerformanceResponse = new AccountPerformance("USD", 0);

        var result = await _sut.GetAccountPerformanceAsync(
            ["DU123"], "1M", TestContext.Current.CancellationToken);

        result.CurrencyType.ShouldBe("USD");
        _fakeApi.LastPerformanceRequest.ShouldNotBeNull();
        _fakeApi.LastPerformanceRequest!.AccountIds[0].ShouldBe("DU123");
        _fakeApi.LastPerformanceRequest.Period.ShouldBe("1M");
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_ConstructsRequestAndDelegates()
    {
        _fakeApi.TransactionResponse = new TransactionHistory("txn1", "USD", 0);

        var result = await _sut.GetTransactionHistoryAsync(
            ["DU123"], ["265598"], "USD", 7, TestContext.Current.CancellationToken);

        result.Id.ShouldBe("txn1");
        _fakeApi.LastTransactionRequest.ShouldNotBeNull();
        _fakeApi.LastTransactionRequest!.AccountIds[0].ShouldBe("DU123");
        _fakeApi.LastTransactionRequest.Conids[0].ShouldBe("265598");
        _fakeApi.LastTransactionRequest.Currency.ShouldBe("USD");
        _fakeApi.LastTransactionRequest.Days.ShouldBe(7);
    }

    [Fact]
    public async Task InvalidatePortfolioCacheAsync_DelegatesToApi()
    {
        await _sut.InvalidatePortfolioCacheAsync("DU123", TestContext.Current.CancellationToken);

        _fakeApi.InvalidateCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetConsolidatedAllocationAsync_ConstructsRequestAndDelegates()
    {
        _fakeApi.ConsolidatedAllocationResponse = new AccountAllocation(null, null, null);

        var result = await _sut.GetConsolidatedAllocationAsync(
            ["DU123", "DU456"], TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        _fakeApi.LastConsolidatedAllocationRequest.ShouldNotBeNull();
        _fakeApi.LastConsolidatedAllocationRequest!.AccountIds[0].ShouldBe("DU123");
        _fakeApi.LastConsolidatedAllocationRequest.AccountIds[1].ShouldBe("DU456");
    }

    [Fact]
    public async Task GetComboPositionsAsync_DelegatesToApi()
    {
        _fakeApi.ComboPositionsResponse = [new ComboPosition("CP.Test", "1*123-1*456", null, null)];

        var result = await _sut.GetComboPositionsAsync("DU123", true, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("CP.Test");
    }

    [Fact]
    public async Task GetRealTimePositionsAsync_DelegatesToApi()
    {
        _fakeApi.PositionsResponse =
        [
            new Position("DU123", 265598, "SPY", 100m, 450.00m, 45000.00m,
                440.00m, 440.00m, 0m, 1000m, "USD", "SPDR S&P 500", "STK",
                null, "SPY", null, true),
        ];

        var result = await _sut.GetRealTimePositionsAsync("DU123",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Ticker.ShouldBe("SPY");
    }

    [Fact]
    public async Task GetSubAccountsAsync_DelegatesToApi()
    {
        _fakeApi.SubAccountsResponse = [new SubAccount("DU123", "DU123", "Paper", "INDIVIDUAL", "DU123")];

        var result = await _sut.GetSubAccountsAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("DU123");
    }

    [Fact]
    public async Task GetSubAccountsPagedAsync_DelegatesToApi()
    {
        _fakeApi.SubAccountsResponse = [new SubAccount("DU123", "DU123", "Paper", "INDIVIDUAL", "DU123")];

        var result = await _sut.GetSubAccountsPagedAsync(0, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("DU123");
    }

    [Fact]
    public async Task GetAllPeriodsPerformanceAsync_ConstructsRequestAndDelegates()
    {
        _fakeApi.AllPeriodsResponse = new AllPeriodsPerformance("base", 0);

        var result = await _sut.GetAllPeriodsPerformanceAsync(
            ["DU123"], TestContext.Current.CancellationToken);

        result.CurrencyType.ShouldBe("base");
        _fakeApi.LastAllPeriodsRequest.ShouldNotBeNull();
        _fakeApi.LastAllPeriodsRequest!.AccountIds[0].ShouldBe("DU123");
    }

    [Fact]
    public async Task GetPartitionedPnlAsync_DelegatesToApi()
    {
        var pnlEntries = new Dictionary<string, PnlEntry>
        {
            ["U1234567.Core"] = new PnlEntry(1, 15.7m, 10000m, 607m, 10000m, 0m),
        };
        _fakeApi.PartitionedPnlResponse = new PartitionedPnl(pnlEntries);

        var result = await _sut.GetPartitionedPnlAsync(TestContext.Current.CancellationToken);

        result.Upnl.ShouldNotBeNull();
        result.Upnl!.ShouldContainKey("U1234567.Core");
        result.Upnl["U1234567.Core"].Dpl.ShouldBe(15.7m);
    }

    private class FakePortfolioApi : IIbkrPortfolioApi
    {
        public List<Position>? PositionsResponse { get; set; }
        public Dictionary<string, AccountSummaryEntry>? SummaryResponse { get; set; }
        public Dictionary<string, LedgerEntry>? LedgerResponse { get; set; }
        public AccountPerformance? PerformanceResponse { get; set; }
        public TransactionHistory? TransactionResponse { get; set; }
        public AccountAllocation? ConsolidatedAllocationResponse { get; set; }
        public List<ComboPosition>? ComboPositionsResponse { get; set; }
        public List<SubAccount>? SubAccountsResponse { get; set; }
        public AllPeriodsPerformance? AllPeriodsResponse { get; set; }
        public PartitionedPnl? PartitionedPnlResponse { get; set; }
        public PerformanceRequest? LastPerformanceRequest { get; private set; }
        public TransactionHistoryRequest? LastTransactionRequest { get; private set; }
        public ConsolidatedAllocationRequest? LastConsolidatedAllocationRequest { get; private set; }
        public AllPeriodsRequest? LastAllPeriodsRequest { get; private set; }
        public bool InvalidateCalled { get; private set; }

        public Task<List<Account>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Account>());

        public Task<List<Position>> GetPositionsAsync(string accountId, int page = 0,
            string? model = null, string? sort = null, string? direction = null,
            string? period = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(PositionsResponse!);

        public Task<Dictionary<string, AccountSummaryEntry>> GetAccountSummaryAsync(
            string accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(SummaryResponse!);

        public Task<Dictionary<string, LedgerEntry>> GetLedgerAsync(
            string accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(LedgerResponse!);

        public Task<AccountInfo> GetAccountInfoAsync(
            string accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AccountInfo("DU123", "DU123", "Test", null, "INDIVIDUAL", "USD"));

        public Task<AccountAllocation> GetAccountAllocationAsync(
            string accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AccountAllocation(null, null, null));

        public Task<List<Position>> GetPositionByConidAsync(
            string accountId, string conid, CancellationToken cancellationToken = default) =>
            Task.FromResult(PositionsResponse ?? new List<Position>());

        public Task<PositionContractInfo> GetPositionAndContractInfoAsync(
            string conid, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PositionContractInfo(265598, "SPY", "SPDR S&P 500", "STK", "ARCA", "USD"));

        public Task InvalidatePortfolioCacheAsync(
            string accountId, CancellationToken cancellationToken = default)
        {
            InvalidateCalled = true;
            return Task.CompletedTask;
        }

        public Task<AccountPerformance> GetAccountPerformanceAsync(
            PerformanceRequest request, CancellationToken cancellationToken = default)
        {
            LastPerformanceRequest = request;
            return Task.FromResult(PerformanceResponse!);
        }

        public Task<TransactionHistory> GetTransactionHistoryAsync(
            TransactionHistoryRequest request, CancellationToken cancellationToken = default)
        {
            LastTransactionRequest = request;
            return Task.FromResult(TransactionResponse!);
        }

        public Task<AccountAllocation> GetConsolidatedAllocationAsync(
            ConsolidatedAllocationRequest request, CancellationToken cancellationToken = default)
        {
            LastConsolidatedAllocationRequest = request;
            return Task.FromResult(ConsolidatedAllocationResponse!);
        }

        public Task<List<ComboPosition>> GetComboPositionsAsync(
            string accountId, bool? nocache = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ComboPositionsResponse!);

        public Task<List<Position>> GetRealTimePositionsAsync(
            string accountId, string? model = null, string? sort = null,
            string? direction = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(PositionsResponse!);

        public Task<List<SubAccount>> GetSubAccountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SubAccountsResponse!);

        public Task<List<SubAccount>> GetSubAccountsPagedAsync(
            int page = 0, CancellationToken cancellationToken = default) =>
            Task.FromResult(SubAccountsResponse!);

        public Task<AllPeriodsPerformance> GetAllPeriodsPerformanceAsync(
            AllPeriodsRequest request, CancellationToken cancellationToken = default)
        {
            LastAllPeriodsRequest = request;
            return Task.FromResult(AllPeriodsResponse!);
        }

        public Task<PartitionedPnl> GetPartitionedPnlAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PartitionedPnlResponse!);
    }
}
