using System;
using System.Net.Http;
using System.Threading.Tasks;
using IbkrConduit.Client;
using IbkrConduit.Flex;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IbkrConduit.Tests.Integration.Flex;

/// <summary>
/// Integration tests for the Flex Web Service two-step query flow.
/// These tests create a <see cref="FlexClient"/> directly (not through TestHarness)
/// because Flex uses its own HTTP pipeline, independent of the consumer Refit pipeline.
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
        // Step 1: SendRequest returns reference code
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

        // Step 2: GetStatement returns report
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

        var flexClient = CreateFlexClient();
        var ops = new FlexOperations(flexClient);

        var result = await ops.ExecuteQueryAsync("12345", TestContext.Current.CancellationToken);

        result.RawXml.ShouldNotBeNull();
        result.Trades.Count.ShouldBe(1);
        result.Trades[0].Symbol.ShouldBe("AAPL");
        result.Trades[0].Quantity.ShouldBe(100m);
        result.OpenPositions.Count.ShouldBe(1);
        result.OpenPositions[0].Symbol.ShouldBe("SPY");
        result.OpenPositions[0].Position.ShouldBe(200m);
    }

    [Fact]
    public async Task ExecuteQueryAsync_PollsOnInProgress_ReturnsOnSecondAttempt()
    {
        // Step 1: SendRequest returns reference code
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

        // Step 2: First GetStatement returns "in progress" (1019)
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

        // Second GetStatement returns data
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

        var flexClient = CreateFlexClient();
        var ops = new FlexOperations(flexClient);

        var result = await ops.ExecuteQueryAsync("67890", TestContext.Current.CancellationToken);

        result.Trades.Count.ShouldBe(1);
        result.Trades[0].Symbol.ShouldBe("GOOG");
    }

    [Fact]
    public async Task ExecuteQueryAsync_ErrorResponse_ThrowsFlexQueryException()
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

        var flexClient = CreateFlexClient();
        var ops = new FlexOperations(flexClient);

        var ex = await Should.ThrowAsync<FlexQueryException>(
            () => ops.ExecuteQueryAsync("BAD_QUERY", TestContext.Current.CancellationToken));

        ex.ErrorCode.ShouldBe(1005);
        ex.Message.ShouldBe("Query ID not found");
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    private FlexClient CreateFlexClient()
    {
        var factory = new FakeHttpClientFactory();
        var baseUrl = _server.Url! + "/AccountManagement/FlexWebService/";
        return new FlexClient(factory, "test-flex", "TEST_TOKEN", NullLogger<FlexClient>.Instance, baseUrl);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
