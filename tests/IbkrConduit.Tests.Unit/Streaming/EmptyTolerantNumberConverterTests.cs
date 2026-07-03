using System.Text.Json;
using IbkrConduit.Streaming;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Streaming;

public class EmptyTolerantNumberConverterTests
{
    // Probe DTO exercising all eight empty-tolerant converters registered on
    // StreamingSerialization.Options. Property-name matching relies on the
    // options' PropertyNameCaseInsensitive=true, so camelCase JSON keys below
    // map onto these PascalCase properties.
    private sealed record NumberProbe
    {
        public decimal? NullableDecimal { get; init; }
        public decimal Decimal { get; init; }
        public int? NullableInt { get; init; }
        public int Int { get; init; }
        public long? NullableLong { get; init; }
        public long Long { get; init; }
        public double? NullableDouble { get; init; }
        public double Double { get; init; }
    }

    private static NumberProbe Deserialize(string json) =>
        JsonDocument.Parse(json).RootElement.Deserialize<NumberProbe>(StreamingSerialization.Options)!;

    // --- decimal? ---

    [Fact]
    public void NullableDecimal_EmptyString_ReturnsNull() =>
        Deserialize("""{"nullableDecimal":""}""").NullableDecimal.ShouldBeNull();

    [Fact]
    public void NullableDecimal_WhitespaceString_ReturnsNull() =>
        Deserialize("""{"nullableDecimal":"   "}""").NullableDecimal.ShouldBeNull();

    [Fact]
    public void NullableDecimal_NumericString_ParsesValue() =>
        Deserialize("""{"nullableDecimal":"740.90"}""").NullableDecimal.ShouldBe(740.90m);

    [Fact]
    public void NullableDecimal_JsonNumber_ParsesValue() =>
        Deserialize("""{"nullableDecimal":740.90}""").NullableDecimal.ShouldBe(740.90m);

    [Fact]
    public void NullableDecimal_JsonNull_ReturnsNull() =>
        Deserialize("""{"nullableDecimal":null}""").NullableDecimal.ShouldBeNull();

    [Fact]
    public void NullableDecimal_BooleanToken_Throws() =>
        Should.Throw<JsonException>(() => Deserialize("""{"nullableDecimal":true}"""));

    [Fact]
    public void NullableDecimal_NonNumericGarbageString_ThrowsJsonException_NotSilentlyNull() =>
        Should.Throw<JsonException>(() => Deserialize("""{"nullableDecimal":"abc"}"""));

    // --- decimal ---

    [Fact]
    public void Decimal_EmptyString_ReturnsZero() =>
        Deserialize("""{"decimal":""}""").Decimal.ShouldBe(0m);

    [Fact]
    public void Decimal_WhitespaceString_ReturnsZero() =>
        Deserialize("""{"decimal":"  "}""").Decimal.ShouldBe(0m);

    [Fact]
    public void Decimal_NumericString_ParsesValue() =>
        Deserialize("""{"decimal":"12.5"}""").Decimal.ShouldBe(12.5m);

    [Fact]
    public void Decimal_JsonNumber_ParsesValue() =>
        Deserialize("""{"decimal":12.5}""").Decimal.ShouldBe(12.5m);

    [Fact]
    public void Decimal_JsonNull_ReturnsZero() =>
        Deserialize("""{"decimal":null}""").Decimal.ShouldBe(0m);

    // --- int? ---

    [Fact]
    public void NullableInt_EmptyString_ReturnsNull() =>
        Deserialize("""{"nullableInt":""}""").NullableInt.ShouldBeNull();

    [Fact]
    public void NullableInt_WhitespaceString_ReturnsNull() =>
        Deserialize("""{"nullableInt":"  "}""").NullableInt.ShouldBeNull();

    [Fact]
    public void NullableInt_NumericString_ParsesValue() =>
        Deserialize("""{"nullableInt":"42"}""").NullableInt.ShouldBe(42);

    [Fact]
    public void NullableInt_JsonNumber_ParsesValue() =>
        Deserialize("""{"nullableInt":42}""").NullableInt.ShouldBe(42);

    [Fact]
    public void NullableInt_JsonNull_ReturnsNull() =>
        Deserialize("""{"nullableInt":null}""").NullableInt.ShouldBeNull();

    [Fact]
    public void NullableInt_BooleanToken_Throws() =>
        Should.Throw<JsonException>(() => Deserialize("""{"nullableInt":true}"""));

    // --- int ---

    [Fact]
    public void Int_EmptyString_ReturnsZero() =>
        Deserialize("""{"int":""}""").Int.ShouldBe(0);

    [Fact]
    public void Int_NumericString_ParsesValue() =>
        Deserialize("""{"int":"7"}""").Int.ShouldBe(7);

    [Fact]
    public void Int_JsonNumber_ParsesValue() =>
        Deserialize("""{"int":7}""").Int.ShouldBe(7);

    [Fact]
    public void Int_JsonNull_ReturnsZero() =>
        Deserialize("""{"int":null}""").Int.ShouldBe(0);

    [Fact]
    public void Int_NonNumericGarbageString_ThrowsJsonException_NotSilentlyZero() =>
        Should.Throw<JsonException>(() => Deserialize("""{"int":"abc"}"""));

    // --- long? ---

    [Fact]
    public void NullableLong_EmptyString_ReturnsNull() =>
        Deserialize("""{"nullableLong":""}""").NullableLong.ShouldBeNull();

    [Fact]
    public void NullableLong_WhitespaceString_ReturnsNull() =>
        Deserialize("""{"nullableLong":"   "}""").NullableLong.ShouldBeNull();

    [Fact]
    public void NullableLong_NumericString_ParsesValue() =>
        Deserialize("""{"nullableLong":"9999999999"}""").NullableLong.ShouldBe(9999999999L);

    [Fact]
    public void NullableLong_JsonNumber_ParsesValue() =>
        Deserialize("""{"nullableLong":9999999999}""").NullableLong.ShouldBe(9999999999L);

    [Fact]
    public void NullableLong_JsonNull_ReturnsNull() =>
        Deserialize("""{"nullableLong":null}""").NullableLong.ShouldBeNull();

    [Fact]
    public void NullableLong_BooleanToken_Throws() =>
        Should.Throw<JsonException>(() => Deserialize("""{"nullableLong":true}"""));

    // --- long ---

    [Fact]
    public void Long_EmptyString_ReturnsZero() =>
        Deserialize("""{"long":""}""").Long.ShouldBe(0L);

    [Fact]
    public void Long_NumericString_ParsesValue() =>
        Deserialize("""{"long":"123456789012"}""").Long.ShouldBe(123456789012L);

    [Fact]
    public void Long_JsonNumber_ParsesValue() =>
        Deserialize("""{"long":123456789012}""").Long.ShouldBe(123456789012L);

    [Fact]
    public void Long_JsonNull_ReturnsZero() =>
        Deserialize("""{"long":null}""").Long.ShouldBe(0L);

    // --- double? ---

    [Fact]
    public void NullableDouble_EmptyString_ReturnsNull() =>
        Deserialize("""{"nullableDouble":""}""").NullableDouble.ShouldBeNull();

    [Fact]
    public void NullableDouble_WhitespaceString_ReturnsNull() =>
        Deserialize("""{"nullableDouble":"   "}""").NullableDouble.ShouldBeNull();

    [Fact]
    public void NullableDouble_NumericString_ParsesValue() =>
        Deserialize("""{"nullableDouble":"3.14"}""").NullableDouble.ShouldBe(3.14);

    [Fact]
    public void NullableDouble_JsonNumber_ParsesValue() =>
        Deserialize("""{"nullableDouble":3.14}""").NullableDouble.ShouldBe(3.14);

    [Fact]
    public void NullableDouble_JsonNull_ReturnsNull() =>
        Deserialize("""{"nullableDouble":null}""").NullableDouble.ShouldBeNull();

    [Fact]
    public void NullableDouble_BooleanToken_Throws() =>
        Should.Throw<JsonException>(() => Deserialize("""{"nullableDouble":true}"""));

    // --- double ---

    [Fact]
    public void Double_EmptyString_ReturnsZero() =>
        Deserialize("""{"double":""}""").Double.ShouldBe(0d);

    [Fact]
    public void Double_NumericString_ParsesValue() =>
        Deserialize("""{"double":"2.5"}""").Double.ShouldBe(2.5);

    [Fact]
    public void Double_JsonNumber_ParsesValue() =>
        Deserialize("""{"double":2.5}""").Double.ShouldBe(2.5);

    [Fact]
    public void Double_JsonNull_ReturnsZero() =>
        Deserialize("""{"double":null}""").Double.ShouldBe(0d);

    // --- Write paths (round-trip through StreamingSerialization.Options) ---

    [Fact]
    public void Serialize_NullableDecimal_WithValue_WritesNumber()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { NullableDecimal = 12.34m }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("NullableDecimal").GetDecimal().ShouldBe(12.34m);
    }

    [Fact]
    public void Serialize_NullableDecimal_Null_WritesJsonNull()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { NullableDecimal = null }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("NullableDecimal").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Serialize_Decimal_WritesNumber()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { Decimal = 5.5m }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("Decimal").GetDecimal().ShouldBe(5.5m);
    }

    [Fact]
    public void Serialize_NullableInt_WithValue_WritesNumber()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { NullableInt = 9 }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("NullableInt").GetInt32().ShouldBe(9);
    }

    [Fact]
    public void Serialize_NullableInt_Null_WritesJsonNull()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { NullableInt = null }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("NullableInt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Serialize_Int_WritesNumber()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { Int = 3 }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("Int").GetInt32().ShouldBe(3);
    }

    [Fact]
    public void Serialize_NullableLong_WithValue_WritesNumber()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { NullableLong = 123456789012L }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("NullableLong").GetInt64().ShouldBe(123456789012L);
    }

    [Fact]
    public void Serialize_NullableLong_Null_WritesJsonNull()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { NullableLong = null }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("NullableLong").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Serialize_Long_WritesNumber()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { Long = 42L }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("Long").GetInt64().ShouldBe(42L);
    }

    [Fact]
    public void Serialize_NullableDouble_WithValue_WritesNumber()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { NullableDouble = 1.5 }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("NullableDouble").GetDouble().ShouldBe(1.5);
    }

    [Fact]
    public void Serialize_NullableDouble_Null_WritesJsonNull()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { NullableDouble = null }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("NullableDouble").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public void Serialize_Double_WritesNumber()
    {
        var json = JsonSerializer.Serialize(new NumberProbe { Double = 6.25 }, StreamingSerialization.Options);
        JsonDocument.Parse(json).RootElement.GetProperty("Double").GetDouble().ShouldBe(6.25);
    }
}
