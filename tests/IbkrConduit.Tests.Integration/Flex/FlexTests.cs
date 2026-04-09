using System;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Errors;
using IbkrConduit.Flex;
using IbkrConduit.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Flex;

/// <summary>
/// Integration tests for the Flex Web Service two-step query flow.
/// These tests construct <see cref="FlexOperations"/> directly (wrapping a <see cref="FlexClient"/>
/// pointed at a WireMock server) because Flex uses its own HTTP pipeline, independent of
/// the consumer Refit pipeline.
/// </summary>
public class FlexTests : IDisposable
{
    private readonly WireMockServer _server;

    public FlexTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task ExecuteQueryAsync_TwoStepFlow_ReturnsParsedResult()
    {
        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/SendRequest")
                .WithParam("q", "12345")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexStatementResponse timestamp="1234567890">
                            <Status>Success</Status>
                            <ReferenceCode>REF_ABC_123</ReferenceCode>
                            <Url>https://example.com</Url>
                        </FlexStatementResponse>
                        """));

        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/GetStatement")
                .WithParam("q", "REF_ABC_123")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexQueryResponse queryName="TestQuery" type="AF">
                          <FlexStatements count="1">
                            <FlexStatement accountId="U1234567">
                              <Trades>
                                <Trade accountId="U1234567" symbol="AAPL" conid="265598"
                                       description="APPLE INC" buySell="BUY" quantity="100"
                                       price="150.50" proceeds="-15050" commission="-1.00"
                                       currency="USD" tradeDate="20260301" tradeTime="093000"
                                       orderType="MKT" exchange="SMART" orderId="ORD1" execId="EX1" />
                              </Trades>
                              <OpenPositions>
                                <OpenPosition accountId="U1234567" symbol="SPY" conid="756733"
                                       description="SPDR S&amp;P 500 ETF" position="200"
                                       markPrice="550.00" positionValue="110000" costBasisMoney="100000"
                                       fifoPnlUnrealized="10000" currency="USD" assetCategory="STK" />
                              </OpenPositions>
                            </FlexStatement>
                          </FlexStatements>
                        </FlexQueryResponse>
                        """));

        var ops = CreateOperations();

        var result = await ops.ExecuteQueryAsync("12345", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RawXml.ShouldNotBeNull();
        result.Value.Trades.Count.ShouldBe(1);
        result.Value.Trades[0].Symbol.ShouldBe("AAPL");
        result.Value.Trades[0].Quantity.ShouldBe(100m);
        result.Value.OpenPositions.Count.ShouldBe(1);
        result.Value.OpenPositions[0].Symbol.ShouldBe("SPY");
        result.Value.OpenPositions[0].Position.ShouldBe(200m);
    }

    [Fact]
    public async Task ExecuteQueryAsync_PollsOnInProgress_ReturnsOnSecondAttempt()
    {
        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/SendRequest")
                .WithParam("q", "67890")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexStatementResponse>
                            <Status>Success</Status>
                            <ReferenceCode>REF_POLL_TEST</ReferenceCode>
                        </FlexStatementResponse>
                        """));

        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/GetStatement")
                .WithParam("q", "REF_POLL_TEST")
                .UsingGet())
            .InScenario("poll")
            .WillSetStateTo("ready")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexStatementResponse>
                            <ErrorCode>1019</ErrorCode>
                            <ErrorMessage>Statement generation in progress</ErrorMessage>
                        </FlexStatementResponse>
                        """));

        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/GetStatement")
                .WithParam("q", "REF_POLL_TEST")
                .UsingGet())
            .InScenario("poll")
            .WhenStateIs("ready")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexQueryResponse>
                          <FlexStatements count="1">
                            <FlexStatement accountId="U9999999">
                              <Trades>
                                <Trade accountId="U9999999" symbol="GOOG" conid="208813720" />
                              </Trades>
                            </FlexStatement>
                          </FlexStatements>
                        </FlexQueryResponse>
                        """));

        var ops = CreateOperations();

        var result = await ops.ExecuteQueryAsync("67890", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Trades.Count.ShouldBe(1);
        result.Value.Trades[0].Symbol.ShouldBe("GOOG");
    }

    [Fact]
    public async Task ExecuteQueryAsync_StrictModeOff_ErrorResponse_ReturnsFlexErrorFailure()
    {
        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/SendRequest")
                .WithParam("q", "BAD_QUERY")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexStatementResponse>
                            <Status>Fail</Status>
                            <ErrorCode>1005</ErrorCode>
                            <ErrorMessage>Query ID not found</ErrorMessage>
                        </FlexStatementResponse>
                        """));

        var ops = CreateOperations(throwOnApiError: false);

        var result = await ops.ExecuteQueryAsync("BAD_QUERY", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        var err = result.Error.ShouldBeOfType<IbkrFlexError>();
        err.ErrorCode.ShouldBe(1005);
        err.Message.ShouldBe("Query ID not found");
    }

    [Fact]
    public async Task ExecuteQueryAsync_StrictModeOn_ErrorResponse_ThrowsIbkrApiException()
    {
        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/SendRequest")
                .WithParam("q", "BAD_QUERY_STRICT")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexStatementResponse>
                            <Status>Fail</Status>
                            <ErrorCode>1005</ErrorCode>
                            <ErrorMessage>Query ID not found</ErrorMessage>
                        </FlexStatementResponse>
                        """));

        var ops = CreateOperations(throwOnApiError: true);

        var ex = await Should.ThrowAsync<IbkrApiException>(
            () => ops.ExecuteQueryAsync("BAD_QUERY_STRICT", TestContext.Current.CancellationToken));

        var err = ex.Error.ShouldBeOfType<IbkrFlexError>();
        err.ErrorCode.ShouldBe(1005);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithDateRange_PassesDatesToRequest()
    {
        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/SendRequest")
                .WithParam("q", "12345")
                .WithParam("fd", "20260101")
                .WithParam("td", "20260331")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexStatementResponse timestamp="1234567890">
                            <Status>Success</Status>
                            <ReferenceCode>REF_DATE_RANGE</ReferenceCode>
                            <Url>https://example.com</Url>
                        </FlexStatementResponse>
                        """));

        _server.Given(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/GetStatement")
                .WithParam("q", "REF_DATE_RANGE")
                .UsingGet())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/xml")
                    .WithBody("""
                        <FlexQueryResponse queryName="DateRangeTest" type="AF">
                          <FlexStatements count="1">
                            <FlexStatement accountId="U1234567">
                              <Trades>
                                <Trade accountId="U1234567" symbol="MSFT" conid="272093"
                                       description="MICROSOFT CORP" buySell="BUY" quantity="50"
                                       price="420.00" proceeds="-21000" commission="-0.50"
                                       currency="USD" tradeDate="20260115" tradeTime="100000"
                                       orderType="LMT" exchange="SMART" orderId="ORD2" execId="EX2" />
                              </Trades>
                            </FlexStatement>
                          </FlexStatements>
                        </FlexQueryResponse>
                        """));

        var ops = CreateOperations();

        var result = await ops.ExecuteQueryAsync("12345", "20260101", "20260331", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Trades.Count.ShouldBe(1);
        result.Value.Trades[0].Symbol.ShouldBe("MSFT");

        var sendRequests = _server.FindLogEntries(
            Request.Create()
                .WithPath("/AccountManagement/FlexWebService/SendRequest")
                .UsingGet());
        sendRequests.Count.ShouldBe(1);
        var requestUrl = sendRequests[0].RequestMessage.Url;
        requestUrl.ShouldContain("fd=20260101");
        requestUrl.ShouldContain("td=20260331");
    }

    [Fact]
    public async Task ExecuteQueryAsync_NoFlexToken_ThrowsInvalidOperationException()
    {
        var ops = new FlexOperations(
            null,
            new IbkrClientOptions(),
            NullLogger<FlexOperations>.Instance);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => ops.ExecuteQueryAsync("12345", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("FlexToken");
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private FlexOperations CreateOperations(bool throwOnApiError = false)
    {
        var factory = new FakeHttpClientFactory();
        var baseUrl = _server.Url! + "/AccountManagement/FlexWebService/";
        var flexClient = new FlexClient(factory, "test-flex", "TEST_TOKEN", NullLogger<FlexClient>.Instance, baseUrl);
        var options = new IbkrClientOptions
        {
            FlexPollTimeout = TimeSpan.FromSeconds(30),
            ThrowOnApiError = throwOnApiError,
        };
        return new FlexOperations(flexClient, options, NullLogger<FlexOperations>.Instance);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
