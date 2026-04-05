using System;
using System.Net;
using System.Threading.Tasks;
using IbkrConduit.Contracts;
using IbkrConduit.Errors;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Contracts;

public class ContractTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task SearchBySymbol_ReturnsResults()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/secdef/search",
            FixtureLoader.LoadBody("Contracts", "GET-secdef-search"));

        var results = (await _harness.Client.Contracts.SearchBySymbolAsync(
            "SPY", TestContext.Current.CancellationToken)).Value;

        results.ShouldNotBeEmpty();
        results.Count.ShouldBe(2);
        var first = results[0];
        first.Conid.ShouldBe(756733);
        first.CompanyName.ShouldBe("SPDR S&P 500 ETF TRUST");
        first.Symbol.ShouldBe("SPY");
        first.Description.ShouldBe("ARCA");
        first.Sections.ShouldNotBeNull();
        first.Sections!.Count.ShouldBe(4);
        first.Sections![0].SecurityType.ShouldBe("STK");
        first.Sections![1].SecurityType.ShouldBe("OPT");
        first.Sections![1].Months.ShouldNotBeNull();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task SearchBySymbol_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/search")
                .UsingGet())
            .InScenario("search-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/search")
                .UsingGet())
            .InScenario("search-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "GET-secdef-search")));

        var results = (await _harness.Client.Contracts.SearchBySymbolAsync(
            "SPY", TestContext.Current.CancellationToken)).Value;

        results.ShouldNotBeEmpty();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetContractDetails_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/contract/756733/info",
            FixtureLoader.LoadBody("Contracts", "GET-contract-info"));

        var details = (await _harness.Client.Contracts.GetContractDetailsAsync(
            "756733", TestContext.Current.CancellationToken)).Value;

        details.ShouldNotBeNull();
        details.Conid.ShouldBe(756733);
        details.Symbol.ShouldBe("SPY");
        details.CompanyName.ShouldBe("SPDR S&P 500 ETF TRUST");
        details.Exchange.ShouldBe("SMART");
        details.Currency.ShouldBe("USD");
        details.InstrumentType.ShouldBe("STK");
        details.ValidExchanges.ShouldContain("ARCA");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetContractDetails_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/756733/info")
                .UsingGet())
            .InScenario("contract-info-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/756733/info")
                .UsingGet())
            .InScenario("contract-info-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "GET-contract-info")));

        var details = (await _harness.Client.Contracts.GetContractDetailsAsync(
            "756733", TestContext.Current.CancellationToken)).Value;

        details.Conid.ShouldBe(756733);
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetOptionStrikes_ReturnsCallsAndPuts()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/secdef/strikes",
            FixtureLoader.LoadBody("Contracts", "GET-secdef-strikes"));

        var strikes = (await _harness.Client.Contracts.GetOptionStrikesAsync(
            "756733", "OPT", "202701", cancellationToken: TestContext.Current.CancellationToken)).Value;

        strikes.ShouldNotBeNull();
        strikes.Call.ShouldNotBeEmpty();
        strikes.Put.ShouldNotBeEmpty();
        strikes.Call.ShouldContain(650.0m);
        strikes.Put.ShouldContain(650.0m);
        strikes.Call.Count.ShouldBe(strikes.Put.Count);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetTradingRules_ReturnsAllFields()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/contract/rules",
            FixtureLoader.LoadBody("Contracts", "POST-contract-rules"));

        var rules = (await _harness.Client.Contracts.GetTradingRulesAsync(
            new TradingRulesRequest(756733, null, true, null, null),
            TestContext.Current.CancellationToken)).Value;

        rules.ShouldNotBeNull();
        rules.DefaultSize.ShouldBe(100m);
        rules.SizeIncrement.ShouldBe(40m);
        rules.CashSize.ShouldBe(0.0m);
        // cashCurrency maps to "cashCurrency" in JSON but IBKR returns "cashCcy" — goes to ExtensionData
        rules.ExtensionData.ShouldNotBeNull();
        rules.ExtensionData!.ShouldContainKey("cashCcy");
        rules.ExtensionData!.ShouldContainKey("orderTypes");
        rules.ExtensionData!.ShouldContainKey("limitPrice");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetTradingRules_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/rules")
                .UsingPost())
            .InScenario("rules-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/rules")
                .UsingPost())
            .InScenario("rules-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "POST-contract-rules")));

        var rules = (await _harness.Client.Contracts.GetTradingRulesAsync(
            new TradingRulesRequest(756733, null, true, null, null),
            TestContext.Current.CancellationToken)).Value;

        rules.DefaultSize.ShouldBe(100m);
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetSecurityDefinitions_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/trsrv/secdef",
            FixtureLoader.LoadBody("Contracts", "GET-trsrv-secdef"));

        var result = (await _harness.Client.Contracts.GetSecurityDefinitionsByConidAsync(
            "756733", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Secdef.ShouldNotBeEmpty();
        var secdef = result.Secdef[0];
        secdef.Conid.ShouldBe(756733);
        secdef.Currency.ShouldBe("USD");
        secdef.Name.ShouldBe("SPDR S&P 500 ETF TRUST");
        secdef.AssetClass.ShouldBe("STK");
        secdef.Ticker.ShouldBe("SPY");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAllConidsByExchange_ReturnsList()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/trsrv/all-conids",
            FixtureLoader.LoadBody("Contracts", "GET-trsrv-all-conids"));

        var conids = (await _harness.Client.Contracts.GetAllConidsByExchangeAsync(
            "NASDAQ", TestContext.Current.CancellationToken)).Value;

        conids.ShouldNotBeEmpty();
        conids.Count.ShouldBe(5);
        conids[0].Ticker.ShouldBe("ADI");
        conids[0].Conid.ShouldBe(4157);
        conids[0].Exchange.ShouldBe("NMS");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetFuturesBySymbol_ReturnsMap()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/trsrv/futures",
            FixtureLoader.LoadBody("Contracts", "GET-trsrv-futures"));

        var futures = (await _harness.Client.Contracts.GetFuturesBySymbolAsync(
            "ES", TestContext.Current.CancellationToken)).Value;

        futures.ShouldContainKey("ES");
        futures["ES"].ShouldNotBeEmpty();
        futures["ES"].Count.ShouldBe(2);
        var first = futures["ES"][0];
        first.Symbol.ShouldBe("ES");
        first.Conid.ShouldBe(515416632);
        first.UnderlyingConid.ShouldBe(11004968);
        first.ExpirationDate.ShouldBe(20261218L);
        first.LastTradingDay.ShouldBe(20261217L);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetStocksBySymbol_ReturnsMap()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/trsrv/stocks",
            FixtureLoader.LoadBody("Contracts", "GET-trsrv-stocks"));

        var stocks = (await _harness.Client.Contracts.GetStocksBySymbolAsync(
            "AAPL", TestContext.Current.CancellationToken)).Value;

        stocks.ShouldContainKey("AAPL");
        stocks["AAPL"].ShouldNotBeEmpty();
        stocks["AAPL"].Count.ShouldBe(2);
        var first = stocks["AAPL"][0];
        first.Name.ShouldBe("APPLE INC");
        first.AssetClass.ShouldBe("STK");
        first.Contracts.ShouldNotBeEmpty();
        first.Contracts[0].Conid.ShouldBe(265598);
        first.Contracts[0].Exchange.ShouldBe("NASDAQ");
        first.Contracts[0].IsUs.ShouldBeTrue();

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetCurrencyPairs_ReturnsMap()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/currency/pairs",
            FixtureLoader.LoadBody("Contracts", "GET-currency-pairs"));

        var pairs = (await _harness.Client.Contracts.GetCurrencyPairsAsync(
            "USD", TestContext.Current.CancellationToken)).Value;

        pairs.ShouldContainKey("USD");
        pairs["USD"].ShouldNotBeEmpty();
        pairs["USD"].Count.ShouldBe(5);
        var chf = pairs["USD"][0];
        chf.Symbol.ShouldBe("USD.CHF");
        chf.Conid.ShouldBe(12087820);
        chf.CcyPair.ShouldBe("CHF");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetExchangeRate_ReturnsRate()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/exchangerate",
            FixtureLoader.LoadBody("Contracts", "GET-exchangerate"));

        var result = (await _harness.Client.Contracts.GetExchangeRateAsync(
            "USD", "EUR", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Rate.ShouldBe(0.86656614m);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetExchangeRate_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/exchangerate")
                .UsingGet())
            .InScenario("exchangerate-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/exchangerate")
                .UsingGet())
            .InScenario("exchangerate-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "GET-exchangerate")));

        var result = (await _harness.Client.Contracts.GetExchangeRateAsync(
            "USD", "EUR", TestContext.Current.CancellationToken)).Value;

        result.Rate.ShouldBe(0.86656614m);
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task SearchBySymbol_ServerError_ReturnsFailureResult()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/search")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"error":"Internal Server Error"}"""));

        var result = await _harness.Client.Contracts.SearchBySymbolAsync(
            "SPY", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<IbkrApiError>();
        error.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
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
