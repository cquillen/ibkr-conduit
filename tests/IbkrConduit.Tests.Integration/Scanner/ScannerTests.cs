using System;
using System.Collections.Generic;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using IbkrConduit.MarketData;
using IbkrConduit.Tests.Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace IbkrConduit.Tests.Integration.Scanner;

public class ScannerTests : IAsyncLifetime, IDisposable
{
    private TestHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        // Override the per-endpoint rate limiters to avoid 15-minute waits for scanner/params
        // during 401 recovery tests that require two requests.
        _harness = await TestHarness.CreateAsync(configureServices: services =>
        {
            services.AddSingleton<IReadOnlyDictionary<string, RateLimiter>>(
                new Dictionary<string, RateLimiter>());
        });
    }

    // --- GetScannerParametersAsync ---

    [Fact]
    public async Task GetScannerParameters_ReturnsParameters()
    {
        _harness.StubAuthenticatedGet(
            "/v1/api/iserver/scanner/params",
            FixtureLoader.LoadBody("Scanner", "GET-scanner-params"));

        var result = (await _harness.Client.MarketData.GetScannerParametersAsync(
            TestContext.Current.CancellationToken)).Value;

        result.ScanTypeList.ShouldNotBeNull();
        result.ScanTypeList!.Count.ShouldBe(2);
        result.ScanTypeList[0].DisplayName.ShouldBe("Top % Gainers");
        result.ScanTypeList[0].Code.ShouldBe("TOP_PERC_GAIN");
        result.ScanTypeList[0].Instruments.ShouldNotBeNull();
        result.ScanTypeList[0].Instruments!.ShouldContain("STK");
        result.ScanTypeList[1].Code.ShouldBe("TOP_PERC_LOSE");

        result.InstrumentList.ShouldNotBeNull();
        result.InstrumentList!.Count.ShouldBe(2);
        result.InstrumentList[0].Type.ShouldBe("STK");

        result.FilterList.ShouldNotBeNull();
        result.FilterList!.Count.ShouldBe(1);
        result.FilterList[0].Code.ShouldBe("afterHoursChangePerc");

        result.LocationTree.ShouldNotBeNull();
        result.LocationTree!.Count.ShouldBe(1);
        result.LocationTree[0].Type.ShouldBe("STK.US");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task GetScannerParameters_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/scanner/params")
                .UsingGet())
            .InScenario("scanner-params-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/scanner/params")
                .UsingGet())
            .InScenario("scanner-params-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Scanner", "GET-scanner-params")));

        var result = (await _harness.Client.MarketData.GetScannerParametersAsync(
            TestContext.Current.CancellationToken)).Value;

        result.ScanTypeList.ShouldNotBeNull();
        result.ScanTypeList!.Count.ShouldBe(2);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- RunScannerAsync ---

    [Fact]
    public async Task RunScanner_ReturnsContracts()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/iserver/scanner/run",
            FixtureLoader.LoadBody("Scanner", "POST-scanner-run"));

        var request = new ScannerRequest("STK", "TOP_PERC_GAIN", "STK.US.MAJOR", null);
        var result = (await _harness.Client.MarketData.RunScannerAsync(
            request, TestContext.Current.CancellationToken)).Value;

        result.Contracts.ShouldNotBeNull();
        result.Contracts!.Count.ShouldBe(3);
        result.Contracts[0].Symbol.ShouldBe("GV");
        result.Contracts[0].ConId.ShouldBe(706149062);
        result.Contracts[0].CompanyName.ShouldBe("VISIONARY HOLDINGS INC");
        result.Contracts[0].ListingExchange.ShouldBe("NASDAQ.SCM");
        result.Contracts[0].SecType.ShouldBe("STK");
        result.Contracts[1].Symbol.ShouldBe("SKYQ");
        result.Contracts[2].Symbol.ShouldBe("TMDE");
        result.ScanDataColumnName.ShouldBe("Chg%");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task RunScanner_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/scanner/run")
                .UsingPost())
            .InScenario("scanner-run-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/iserver/scanner/run")
                .UsingPost())
            .InScenario("scanner-run-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Scanner", "POST-scanner-run")));

        var request = new ScannerRequest("STK", "TOP_PERC_GAIN", "STK.US.MAJOR", null);
        var result = (await _harness.Client.MarketData.RunScannerAsync(
            request, TestContext.Current.CancellationToken)).Value;

        result.Contracts.ShouldNotBeNull();
        result.Contracts!.Count.ShouldBe(3);

        _harness.VerifyReauthenticationOccurred();
    }

    // --- RunHmdsScannerAsync ---

    [Fact]
    public async Task RunHmdsScanner_ReturnsContracts()
    {
        _harness.StubAuthenticatedPost(
            "/v1/api/hmds/scanner",
            FixtureLoader.LoadBody("Scanner", "POST-hmds-scanner"));

        var request = new HmdsScannerRequest("STK", "STK.US.MAJOR", "TOP_PERC_GAIN", "STK", 10, null);
        var result = (await _harness.Client.MarketData.RunHmdsScannerAsync(
            request, TestContext.Current.CancellationToken)).Value;

        result.Total.ShouldBe("250");
        result.Size.ShouldBe("2");
        result.Offset.ShouldBe("0");
        result.ScanTime.ShouldBe("20260404-07:10:12");
        result.Id.ShouldBe("scanner-001");
        result.Contracts.ShouldNotBeNull();
        result.Contracts!.Contract.ShouldNotBeNull();
        result.Contracts!.Contract!.Count.ShouldBe(2);
        result.Contracts!.Contract[0].ContractId.ShouldBe("706149062");
        result.Contracts!.Contract[1].ContractId.ShouldBe("863831941");

        _harness.VerifyUserAgentOnAllRequests();
        _harness.VerifyHandshakeOccurred();
    }

    [Fact]
    public async Task RunHmdsScanner_401Recovery_ReauthenticatesAndRetries()
    {
        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/hmds/scanner")
                .UsingPost())
            .InScenario("hmds-scanner-401-recovery")
            .WillSetStateTo("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithBody("Unauthorized"));

        _harness.Server.Given(
            Request.Create()
                .WithPath("/v1/api/hmds/scanner")
                .UsingPost())
            .InScenario("hmds-scanner-401-recovery")
            .WhenStateIs("token-expired")
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(FixtureLoader.LoadBody("Scanner", "POST-hmds-scanner")));

        var request = new HmdsScannerRequest("STK", "STK.US.MAJOR", "TOP_PERC_GAIN", "STK", 10, null);
        var result = (await _harness.Client.MarketData.RunHmdsScannerAsync(
            request, TestContext.Current.CancellationToken)).Value;

        result.Contracts.ShouldNotBeNull();

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
