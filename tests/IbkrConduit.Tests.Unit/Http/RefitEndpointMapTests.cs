using System.Net.Http;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class RefitEndpointMapTests
{
    [Fact]
    public void Build_SingleInterface_MapsSimpleEndpoint()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/test/abc123");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestDto));
        result.IsCollection.ShouldBeFalse();
        result.IsDictionary.ShouldBeFalse();
    }

    [Fact]
    public void Build_ListReturnType_UnwrapsToElementType()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/test/items");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestItem));
        result.IsCollection.ShouldBeTrue();
    }

    [Fact]
    public void Build_DictionaryReturnType_UnwrapsToValueType()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/test/U1234567/values");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestValue));
        result.IsDictionary.ShouldBeTrue();
    }

    [Fact]
    public void Build_VoidReturn_SkippedInMap()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Post, "/v1/api/test/action");

        result.ShouldBeNull();
    }

    [Fact]
    public void Build_DeleteMethod_MapsCorrectly()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Delete, "/v1/api/test/xyz789");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestDto));
    }

    [Fact]
    public void Build_IApiResponseString_SkippedInMap()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApiWithRawResponse)]);

        var result = map.TryGetDtoType(HttpMethod.Post, "/v1/api/test/raw");

        result.ShouldBeNull();
    }

    [Fact]
    public void Build_UnknownPath_ReturnsNull()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/unknown/path");

        result.ShouldBeNull();
    }

    [Fact]
    public void Build_WrongHttpMethod_ReturnsNull()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        // /v1/api/test/{id} is registered for GET, not POST
        var result = map.TryGetDtoType(HttpMethod.Post, "/v1/api/test/abc123");

        result.ShouldBeNull();
    }

    [Fact]
    public void Build_PathWithMultipleParams_MatchesCorrectly()
    {
        var map = RefitEndpointMap.Build([typeof(ITestApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/test/U1234567/values");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(TestValue));
    }

    [Fact]
    public void Build_RealPortfolioInterface_MapsPositionsEndpoint()
    {
        var map = RefitEndpointMap.Build([typeof(IbkrConduit.Portfolio.IIbkrPortfolioApi)]);

        var result = map.TryGetDtoType(HttpMethod.Get, "/v1/api/portfolio/U1234567/positions/0");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(IbkrConduit.Portfolio.Position));
        result.IsCollection.ShouldBeTrue();
    }

    [Fact]
    public void Build_RealPortfolioInterface_MapsSummaryEndpoint()
    {
        var map = RefitEndpointMap.Build([typeof(IbkrConduit.Portfolio.IIbkrPortfolioApi)]);

        var result = map.TryGetDtoType(
            HttpMethod.Get, "/v1/api/portfolio/U1234567/summary");

        result.ShouldNotBeNull();
        result.DtoType.ShouldBe(typeof(IbkrConduit.Portfolio.AccountSummaryEntry));
        result.IsDictionary.ShouldBeTrue();
    }

    [Fact]
    public void Build_MultipleInterfaces_AllMapped()
    {
        var map = RefitEndpointMap.Build([
            typeof(IbkrConduit.Portfolio.IIbkrPortfolioApi),
            typeof(IbkrConduit.Accounts.IIbkrAccountApi),
        ]);

        map.TryGetDtoType(HttpMethod.Get, "/v1/api/portfolio/accounts").ShouldNotBeNull();
        map.TryGetDtoType(HttpMethod.Get, "/v1/api/iserver/accounts").ShouldNotBeNull();
    }
}
