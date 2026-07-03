using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IbkrConduit.Serialization;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Serialization;

/// <summary>
/// Guards the JSON number-handling behavior the library depends on. IbkrConduit registers every
/// Refit client via <c>AddRefitClient&lt;TApi&gt;(IbkrRefitSettings.Create())</c>, so it relies on
/// the shared <see cref="IbkrRefitSettings"/> serializer rather than Refit's bare default. That
/// serializer must (a) keep reading numeric fields IBKR returns as JSON strings (Refit 12's
/// <c>JsonNumberHandling.AllowReadingFromString</c> default, which IBKR relies on heavily) and
/// (b) additionally tolerate empty/whitespace numeric strings by mapping them to <c>null</c>
/// (nullable) or <c>0</c> (non-nullable) instead of throwing mid-response. These tests assert
/// against the library's actual settings so a regression fails here instead of silently breaking
/// deserialization at runtime.
/// </summary>
public class RefitSerializerDefaultsTests
{
    private sealed record NumberModel(int Count, decimal Ratio);

    private sealed record NullableNumberModel(int? Count, decimal? Ratio);

    private static Task<T?> DeserializeAsync<T>(string json)
    {
        // Exercise the exact ContentSerializer every Refit client in the library uses.
        var serializer = IbkrRefitSettings.Create().ContentSerializer;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return serializer.FromHttpContentAsync<T>(content, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LibrarySerializer_ReadsNumbersFromJsonStrings_WithoutPerPropertyAnnotation()
    {
        var result = await DeserializeAsync<NumberModel>("""{"Count":"42","Ratio":"3.14"}""");

        result.ShouldNotBeNull();
        result!.Count.ShouldBe(42);
        result.Ratio.ShouldBe(3.14m);
    }

    [Fact]
    public async Task LibrarySerializer_ReadsPlainJsonNumbers()
    {
        var result = await DeserializeAsync<NumberModel>("""{"Count":42,"Ratio":3.14}""");

        result.ShouldNotBeNull();
        result!.Count.ShouldBe(42);
        result.Ratio.ShouldBe(3.14m);
    }

    [Fact]
    public async Task LibrarySerializer_EmptyString_MapsNullableNumbersToNull()
    {
        var result = await DeserializeAsync<NullableNumberModel>("""{"Count":"","Ratio":""}""");

        result.ShouldNotBeNull();
        result!.Count.ShouldBeNull();
        result.Ratio.ShouldBeNull();
    }

    [Fact]
    public async Task LibrarySerializer_WhitespaceString_MapsNullableNumbersToNull()
    {
        var result = await DeserializeAsync<NullableNumberModel>("""{"Count":"  ","Ratio":"   "}""");

        result.ShouldNotBeNull();
        result!.Count.ShouldBeNull();
        result.Ratio.ShouldBeNull();
    }

    [Fact]
    public async Task LibrarySerializer_EmptyString_MapsNonNullableNumbersToZero_NotThrow()
    {
        var result = await DeserializeAsync<NumberModel>("""{"Count":"","Ratio":""}""");

        result.ShouldNotBeNull();
        result!.Count.ShouldBe(0);
        result.Ratio.ShouldBe(0m);
    }

    [Fact]
    public async Task LibrarySerializer_NullableNumericString_StillParsesValue()
    {
        var result = await DeserializeAsync<NullableNumberModel>("""{"Count":"7","Ratio":"2.5"}""");

        result.ShouldNotBeNull();
        result!.Count.ShouldBe(7);
        result.Ratio.ShouldBe(2.5m);
    }
}
