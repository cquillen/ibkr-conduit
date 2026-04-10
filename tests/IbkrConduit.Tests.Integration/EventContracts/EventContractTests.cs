using System;
using System.Threading.Tasks;
using IbkrConduit.Tests.Integration.Fixtures;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.EventContracts;

public class EventContractTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await TestHarness.CreateAsync();
    }

    [Fact]
    public async Task GetCategoryTree_Success_ReturnsCategoriesWithMarkets()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/forecast/category/tree",
            FixtureLoader.LoadBody("EventContracts", "GET-category-tree"));

        var result = (await _harness.Client.EventContracts.GetCategoryTreeAsync(TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Categories.ShouldNotBeNull();
        result.Categories.Count.ShouldBe(3);
        result.Categories.ShouldContainKey("g78664");

        var northeast = result.Categories["g78664"];
        northeast.Name.ShouldBe("Northeast");
        northeast.ParentId.ShouldBe("g17457");
        northeast.Markets.ShouldNotBeNull();
        northeast.Markets!.Count.ShouldBe(1);
        northeast.Markets[0].Name.ShouldBe("Northeastern US CPI");
        northeast.Markets[0].Symbol.ShouldBe("RCNET");
        northeast.Markets[0].Exchange.ShouldBe("FORECASTX");
        northeast.Markets[0].Conid.ShouldBe(831072285);
        northeast.Markets[0].ProductConid.ShouldBe(831072289);

        var agriculture = result.Categories["g22973"];
        agriculture.Name.ShouldBe("Agriculture");
        agriculture.Markets.ShouldBeNull();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetMarket_Success_ReturnsContractsList()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/forecast/contract/market",
            FixtureLoader.LoadBody("EventContracts", "GET-market"));

        var result = (await _harness.Client.EventContracts.GetMarketAsync(658663572, cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.MarketName.ShouldBe("US Fed Funds Target Rate");
        result.Exchange.ShouldBe("FORECASTX");
        result.Symbol.ShouldBe("FF");
        result.LogoCategory.ShouldBe("g13834");
        result.ExcludeHistoricalData.ShouldBeFalse();
        result.Payout.ShouldBe(1.0);
        result.Contracts.ShouldNotBeNull();
        result.Contracts.Count.ShouldBe(2);

        var yesContract = result.Contracts[0];
        yesContract.Conid.ShouldBe(722489372);
        yesContract.Side.ShouldBe("Y");
        yesContract.Expiration.ShouldBe("20270127");
        yesContract.Strike.ShouldBe(3.125);
        yesContract.StrikeLabel.ShouldBe("Above 3.125%");
        yesContract.ExpiryLabel.ShouldBe("January 27, 2027");
        yesContract.UnderlyingConid.ShouldBe(658663572);
        yesContract.TimeSpecifier.ShouldBe("2027.1.28");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetContractRules_Success_ReturnsRulesDetails()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/forecast/contract/rules",
            FixtureLoader.LoadBody("EventContracts", "GET-contract-rules"));

        var result = (await _harness.Client.EventContracts.GetContractRulesAsync(722489372, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.AssetClass.ShouldBe("OPT");
        result.MarketName.ShouldBe("US Fed Funds Target Rate");
        result.Threshold.ShouldBe("3.125");
        result.SourceAgency.ShouldBe("U.S. Federal Reserve");
        result.ProductCode.ShouldBe("FF");
        result.Payout.ShouldBe("$1.00");
        result.PriceIncrement.ShouldBe("$0.01");
        result.ExchangeTimezone.ShouldBe("US/Central");
        result.LastTradeTime.ShouldBe(1801076400);
        result.ReleaseTime.ShouldBe(1801076400);
        result.PayoutTime.ShouldBe(1801162800);

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetContractDetails_Success_ReturnsYesNoConids()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/forecast/contract/details",
            FixtureLoader.LoadBody("EventContracts", "GET-contract-details"));

        var result = (await _harness.Client.EventContracts.GetContractDetailsAsync(722489372, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.ConidYes.ShouldBe(722489372);
        result.ConidNo.ShouldBe(722489376);
        result.Question.ShouldContain("US Fed Funds Target Rate");
        result.Side.ShouldBe("Y");
        result.StrikeLabel.ShouldBe("Above 3.125%");
        result.Strike.ShouldBe(3.125);
        result.Exchange.ShouldBe("FORECASTX");
        result.Expiration.ShouldBe("20270127");
        result.Symbol.ShouldBe("FF");
        result.Category.ShouldBe("g1003");
        result.MarketName.ShouldBe("US Fed Funds Target Rate");
        result.UnderlyingConid.ShouldBe(658663572);
        result.PayoutAmount.ShouldBe(1.0);
        result.ProductConid.ShouldBe(658663579);
        result.IsRestricted.ShouldBeFalse();

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetContractSchedules_Success_ReturnsTradingSchedules()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/forecast/contract/schedules",
            FixtureLoader.LoadBody("EventContracts", "GET-contract-schedules"));

        var result = (await _harness.Client.EventContracts.GetContractSchedulesAsync(722489372, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Timezone.ShouldBe("US/Central");
        result.TradingSchedules.ShouldNotBeNull();
        result.TradingSchedules.Count.ShouldBe(2);

        var saturday = result.TradingSchedules[0];
        saturday.DayOfWeek.ShouldBe("Saturday");
        saturday.TradingTimes.Count.ShouldBe(2);
        saturday.TradingTimes[0].Open.ShouldBe("12:00 AM");
        saturday.TradingTimes[0].Close.ShouldBe("4:15 PM");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetCategoryTree_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/category/tree")
                .UsingGet())
            .InScenario("category-tree-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/category/tree")
                .UsingGet())
            .InScenario("category-tree-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("EventContracts", "GET-category-tree")));

        var result = (await _harness.Client.EventContracts.GetCategoryTreeAsync(TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Categories.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetMarket_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/contract/market")
                .UsingGet())
            .InScenario("market-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/contract/market")
                .UsingGet())
            .InScenario("market-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("EventContracts", "GET-market")));

        var result = (await _harness.Client.EventContracts.GetMarketAsync(658663572, cancellationToken: TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Contracts.ShouldNotBeEmpty();

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetContractRules_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/contract/rules")
                .UsingGet())
            .InScenario("rules-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/contract/rules")
                .UsingGet())
            .InScenario("rules-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("EventContracts", "GET-contract-rules")));

        var result = (await _harness.Client.EventContracts.GetContractRulesAsync(722489372, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.MarketName.ShouldBe("US Fed Funds Target Rate");

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetContractDetails_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/contract/details")
                .UsingGet())
            .InScenario("details-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/contract/details")
                .UsingGet())
            .InScenario("details-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("EventContracts", "GET-contract-details")));

        var result = (await _harness.Client.EventContracts.GetContractDetailsAsync(722489372, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.ConidYes.ShouldBe(722489372);

        _harness.VerifyReauthenticationOccurred();
    }

    [Fact]
    public async Task GetContractSchedules_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/contract/schedules")
                .UsingGet())
            .InScenario("schedules-401")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/forecast/contract/schedules")
                .UsingGet())
            .InScenario("schedules-401")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("EventContracts", "GET-contract-schedules")));

        var result = (await _harness.Client.EventContracts.GetContractSchedulesAsync(722489372, TestContext.Current.CancellationToken)).Value;

        result.ShouldNotBeNull();
        result.Timezone.ShouldBe("US/Central");

        _harness.VerifyReauthenticationOccurred();
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
