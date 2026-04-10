using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Contracts;
using IbkrConduit.Session;
using IbkrConduit.Tests.Unit.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Contracts;

public class ContractOperationsTests
{
    private readonly IIbkrContractApi _api = Substitute.For<IIbkrContractApi>();
    private readonly ContractOperations _sut;

    public ContractOperationsTests() => _sut = new ContractOperations(_api, new IbkrClientOptions(), NullLogger<ContractOperations>.Instance);

    [Fact]
    public async Task SearchBySymbolAsync_DelegatesToApi()
    {
        var expected = new List<ContractSearchResult>
        {
            new(265598, "AAPL Inc.", "AAPL", "Apple Inc.", "AAPL", "265598", "STK", "NASDAQ", null),
        };
        _api.SearchBySymbolAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<bool?>(), Arg.Any<bool?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.SearchBySymbolAsync("AAPL", cancellationToken: TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).SearchBySymbolAsync("AAPL", null, null, null, null, null, null, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetContractDetailsAsync_DelegatesToApi()
    {
        var expected = new ContractDetails(265598, "AAPL", "Apple Inc.", "SMART", "NASDAQ", "USD", "STK", "SMART,NASDAQ");
        _api.GetContractDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetContractDetailsAsync("265598", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetContractDetailsAsync("265598", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetSecurityDefinitionInfoAsync_DelegatesToApi()
    {
        var expected = new List<SecurityDefinitionInfo>
        {
            new(265598, "AAPL", "OPT", "CBOE", "CBOE", "C", "150", "20241220"),
        };
        _api.GetSecurityDefinitionInfoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<OptionRight?>(), Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetSecurityDefinitionInfoAsync(
            "265598", "OPT", "DEC2024",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetSecurityDefinitionInfoAsync(
            "265598", "OPT", "DEC2024",
            null, null, null, null, null,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetOptionStrikesAsync_DelegatesToApi()
    {
        var expected = new OptionStrikes(new List<decimal> { 150m, 155m }, new List<decimal> { 145m, 150m });
        _api.GetOptionStrikesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetOptionStrikesAsync("265598", "OPT", "DEC2024", cancellationToken: TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetOptionStrikesAsync(
            "265598", "OPT", "DEC2024", null,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetTradingRulesAsync_DelegatesToApi()
    {
        var request = new TradingRulesRequest(265598, null, null, null, null);
        var expected = new TradingRules(1m, 1m, 0m, "USD");
        _api.GetTradingRulesAsync(Arg.Any<TradingRulesRequest>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetTradingRulesAsync(request, TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetTradingRulesAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetSecurityDefinitionsByConidAsync_DelegatesToApi()
    {
        var expected = new SecurityDefinitionResponse(new List<SecurityDefinition>());
        _api.GetSecurityDefinitionsByConidAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetSecurityDefinitionsByConidAsync("265598", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetSecurityDefinitionsByConidAsync("265598", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetAllConidsByExchangeAsync_DelegatesToApi()
    {
        var expected = new List<ExchangeConid> { new("AAPL", 265598, "NASDAQ") };
        _api.GetAllConidsByExchangeAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetAllConidsByExchangeAsync("NASDAQ", cancellationToken: TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetAllConidsByExchangeAsync("NASDAQ", null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetFuturesBySymbolAsync_DelegatesToApi()
    {
        var expected = new Dictionary<string, List<FutureContract>>
        {
            ["ES"] = new List<FutureContract> { new("ES", 495512551, 11004968, 20241220L, 20241220L) },
        };
        _api.GetFuturesBySymbolAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetFuturesBySymbolAsync("ES", cancellationToken: TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetFuturesBySymbolAsync("ES", null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetStocksBySymbolAsync_DelegatesToApi()
    {
        var expected = new Dictionary<string, List<StockContract>>
        {
            ["AAPL"] = new List<StockContract> { new("Apple Inc.", null, "STK", new List<StockContractDetail>()) },
        };
        _api.GetStocksBySymbolAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetStocksBySymbolAsync("AAPL", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetStocksBySymbolAsync("AAPL", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetTradingScheduleAsync_DelegatesToApi()
    {
        var expected = new List<TradingSchedule> { new("SCH1", new List<TradeTiming>()) };
        _api.GetTradingScheduleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetTradingScheduleAsync(
            "STK", "AAPL", "265598",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetTradingScheduleAsync(
            "STK", "AAPL", "265598",
            null, null,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetCurrencyPairsAsync_DelegatesToApi()
    {
        var expected = new Dictionary<string, List<CurrencyPair>>
        {
            ["EUR"] = new List<CurrencyPair> { new("EUR.USD", 12087792, "EUR") },
        };
        _api.GetCurrencyPairsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetCurrencyPairsAsync("EUR", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetCurrencyPairsAsync("EUR", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetExchangeRateAsync_DelegatesToApi()
    {
        var expected = new ExchangeRateResponse(1.08m);
        _api.GetExchangeRateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FakeApiResponse.Success(expected));

        var result = await _sut.GetExchangeRateAsync("USD", "EUR", TestContext.Current.CancellationToken);

        result.Value.ShouldBeSameAs(expected);
        await _api.Received(1).GetExchangeRateAsync("USD", "EUR", TestContext.Current.CancellationToken);
    }
}
