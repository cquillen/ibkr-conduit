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
            "SPY", cancellationToken: TestContext.Current.CancellationToken)).Value;

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
            "SPY", cancellationToken: TestContext.Current.CancellationToken)).Value;

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
            "756733", SecurityType.Option, new ExpiryMonth(2027, 1), cancellationToken: TestContext.Current.CancellationToken)).Value;

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
            "NASDAQ", cancellationToken: TestContext.Current.CancellationToken)).Value;

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
            "ES", cancellationToken: TestContext.Current.CancellationToken)).Value;

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
    public async Task GetContractInfoAndRules_ReturnsAllFields()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/contract/265598/info-and-rules",
            FixtureLoader.LoadBody("Contracts", "GET-contract-info-and-rules"));

        var info = (await _harness.Client.Contracts.GetContractInfoAndRulesAsync(
            "265598", isBuy: true, cancellationToken: TestContext.Current.CancellationToken)).Value;

        info.ShouldNotBeNull();
        info.ConId.ShouldBe(265598);
        info.Symbol.ShouldBe("AAPL");
        info.CompanyName.ShouldBe("APPLE INC");
        info.Exchange.ShouldBe("SMART");
        info.Currency.ShouldBe("USD");
        info.InstrumentType.ShouldBe("STK");
        info.ValidExchanges.ShouldContain("NASDAQ");
        info.Rules.ShouldNotBeNull();
        info.Rules!.DefaultSize.ShouldBe(100m);
        info.Rules!.SizeIncrement.ShouldBe(100m);
        info.Rules!.OrderTypes.ShouldNotBeNull();
        info.Rules!.OrderTypes!.ShouldContain("limit");
        info.Rules!.AlgoEligible.ShouldBe(true);
        info.Rules!.CashCcy.ShouldBe("USD");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetContractInfoAndRules_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/265598/info-and-rules")
                .UsingGet())
            .InScenario("info-and-rules-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/265598/info-and-rules")
                .UsingGet())
            .InScenario("info-and-rules-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "GET-contract-info-and-rules")));

        var info = (await _harness.Client.Contracts.GetContractInfoAndRulesAsync(
            "265598", isBuy: true, cancellationToken: TestContext.Current.CancellationToken)).Value;

        info.ConId.ShouldBe(265598);
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetAlgos_ReturnsAlgoList()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/contract/265598/algos",
            FixtureLoader.LoadBody("Contracts", "GET-contract-algos"));

        var result = (await _harness.Client.Contracts.GetAlgosAsync(
            "265598", addDescription: 1, addParams: 1, cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Algos.ShouldNotBeNull();
        result.Algos!.Count.ShouldBe(3);
        var adaptive = result.Algos[0];
        adaptive.Name.ShouldBe("Adaptive");
        adaptive.Id.ShouldBe("Adaptive");
        adaptive.Parameters.ShouldNotBeNull();
        adaptive.Parameters!.Count.ShouldBe(1);
        adaptive.Parameters![0].Id.ShouldBe("adaptivePriority");
        adaptive.Parameters![0].Required.ShouldBe("true");
        adaptive.Parameters![0].LegalStrings.ShouldNotBeNull();
        adaptive.Parameters![0].LegalStrings!.ShouldContain("Urgent");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetAlgos_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/265598/algos")
                .UsingGet())
            .InScenario("algos-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/contract/265598/algos")
                .UsingGet())
            .InScenario("algos-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "GET-contract-algos")));

        var result = (await _harness.Client.Contracts.GetAlgosAsync(
            "265598", addDescription: 1, addParams: 1, cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.Algos.ShouldNotBeNull();
        result.Algos!.Count.ShouldBe(3);
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetBondFilters_ReturnsFilters()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/secdef/bond-filters",
            FixtureLoader.LoadBody("Contracts", "GET-bond-filters"));

        var result = (await _harness.Client.Contracts.GetBondFiltersAsync(
            "BOND", "e1400715", TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.BondFilters.ShouldNotBeNull();
        result.BondFilters!.Count.ShouldBe(2);
        var maturity = result.BondFilters[0];
        maturity.DisplayText.ShouldBe("Maturity Date");
        maturity.ColumnId.ShouldBe(27);
        maturity.Options.ShouldNotBeNull();
        maturity.Options!.Count.ShouldBe(2);
        maturity.Options![0].Text.ShouldBe("Dec 2028");
        maturity.Options![0].Value.ShouldBe("202812");
        var currency = result.BondFilters[1];
        currency.DisplayText.ShouldBe("Currency");
        currency.Options!.ShouldContain(o => o.Value == "USD");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetBondFilters_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/bond-filters")
                .UsingGet())
            .InScenario("bond-filters-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/bond-filters")
                .UsingGet())
            .InScenario("bond-filters-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "GET-bond-filters")));

        var result = (await _harness.Client.Contracts.GetBondFiltersAsync(
            "BOND", "e1400715", TestContext.Current.CancellationToken)).Value;

        result.BondFilters.ShouldNotBeNull();
        result.BondFilters!.Count.ShouldBe(2);
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task SearchBySymbolPost_ReturnsResults()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/secdef/search",
            FixtureLoader.LoadBody("Contracts", "POST-secdef-search"));

        var results = (await _harness.Client.Contracts.SearchBySymbolPostAsync(
            new ContractSearchRequest("AAPL"), TestContext.Current.CancellationToken)).Value;

        results.ShouldNotBeEmpty();
        results.Count.ShouldBe(2);
        var first = results[0];
        first.Conid.ShouldBe(265598);
        first.CompanyName.ShouldBe("APPLE INC");
        first.Symbol.ShouldBe("AAPL");
        first.Description.ShouldBe("NASDAQ");
        first.Sections.ShouldNotBeNull();
        first.Sections!.Count.ShouldBe(2);
        first.Sections![0].SecurityType.ShouldBe("STK");

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task SearchBySymbolPost_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/search")
                .UsingPost())
            .InScenario("post-search-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/secdef/search")
                .UsingPost())
            .InScenario("post-search-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "POST-secdef-search")));

        var results = (await _harness.Client.Contracts.SearchBySymbolPostAsync(
            new ContractSearchRequest("AAPL"), TestContext.Current.CancellationToken)).Value;

        results.ShouldNotBeEmpty();
        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetTradingScheduleNew_ReturnsSchedule()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/contract/trading-schedule",
            FixtureLoader.LoadBody("Contracts", "GET-trading-schedule"));

        var result = (await _harness.Client.Contracts.GetTradingScheduleNewAsync(
            "756733", cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.ExchangeTimeZone.ShouldBe("US/Eastern");
        result.Schedules.ShouldNotBeNull();
        result.Schedules!.Count.ShouldBe(2);
        result.Schedules.ShouldContainKey("20260402");
        var day = result.Schedules["20260402"];
        day.LiquidHours.ShouldNotBeNull();
        day.LiquidHours!.Count.ShouldBe(1);
        day.LiquidHours![0].Opening.ShouldBe(1775136600L);
        day.LiquidHours![0].Closing.ShouldBe(1775160000L);
        day.ExtendedHours.ShouldNotBeNull();
        day.ExtendedHours!.Count.ShouldBe(1);
        day.ExtendedHours![0].CancelDailyOrders.ShouldBe(true);

        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetTradingScheduleNew_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/contract/trading-schedule")
                .UsingGet())
            .InScenario("trading-schedule-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/contract/trading-schedule")
                .UsingGet())
            .InScenario("trading-schedule-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Contracts", "GET-trading-schedule")));

        var result = (await _harness.Client.Contracts.GetTradingScheduleNewAsync(
            "756733", cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ExchangeTimeZone.ShouldBe("US/Eastern");
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
            "SPY", cancellationToken: TestContext.Current.CancellationToken);

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
