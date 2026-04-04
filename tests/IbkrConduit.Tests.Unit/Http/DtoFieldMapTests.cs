using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using IbkrConduit.Http;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

public class DtoFieldMapTests
{
    // --- Test DTOs ---

    [ExcludeFromCodeCoverage]
    public record SimpleDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("age")] int Age);

    [ExcludeFromCodeCoverage]
    public record DtoWithNullables(
        [property: JsonPropertyName("required_field")] string RequiredField,
        [property: JsonPropertyName("nullable_ref")] string? NullableRef,
        [property: JsonPropertyName("nullable_value")] int? NullableValue,
        [property: JsonPropertyName("with_default")] string? WithDefault = null);

    [ExcludeFromCodeCoverage]
    public record DtoWithExtensionData(
        [property: JsonPropertyName("id")] string Id)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; init; }
    }

    [ExcludeFromCodeCoverage]
    public record NestedChild(
        [property: JsonPropertyName("childField")] string ChildField);

    [ExcludeFromCodeCoverage]
    public record DtoWithNestedType(
        [property: JsonPropertyName("topField")] string TopField,
        [property: JsonPropertyName("child")] NestedChild? Child);

    [ExcludeFromCodeCoverage]
    public sealed record ClassStyleDto
    {
        [JsonPropertyName("prop_a")]
        public string PropA { get; init; } = string.Empty;

        [JsonPropertyName("prop_b")]
        public int? PropB { get; init; }
    }

    // --- Tests ---

    [Fact]
    public void Extract_SimpleRecord_ReturnsAllFieldNames()
    {
        var result = DtoFieldMap.Extract(typeof(SimpleDto));

        result.FieldNames.ShouldBe(new[] { "name", "age" }, ignoreOrder: true);
    }

    [Fact]
    public void Extract_SimpleRecord_HasNoExtensionData()
    {
        var result = DtoFieldMap.Extract(typeof(SimpleDto));

        result.HasExtensionData.ShouldBeFalse();
    }

    [Fact]
    public void Extract_NullableFields_MarkedAsOptional()
    {
        var result = DtoFieldMap.Extract(typeof(DtoWithNullables));

        result.IsOptional("required_field").ShouldBeFalse();
        result.IsOptional("nullable_ref").ShouldBeTrue();
        result.IsOptional("nullable_value").ShouldBeTrue();
        result.IsOptional("with_default").ShouldBeTrue();
    }

    [Fact]
    public void Extract_WithExtensionData_DetectedCorrectly()
    {
        var result = DtoFieldMap.Extract(typeof(DtoWithExtensionData));

        result.HasExtensionData.ShouldBeTrue();
        result.FieldNames.ShouldBe(new[] { "id" });
    }

    [Fact]
    public void Extract_ExtensionDataProperty_NotIncludedInFieldNames()
    {
        var result = DtoFieldMap.Extract(typeof(DtoWithExtensionData));

        result.FieldNames.ShouldNotContain("AdditionalData");
    }

    [Fact]
    public void Extract_NestedType_IncludesNestedFieldsWithDotNotation()
    {
        var result = DtoFieldMap.Extract(typeof(DtoWithNestedType));

        result.FieldNames.ShouldContain("topField");
        result.FieldNames.ShouldContain("child");
        result.NestedMaps.ShouldContainKey("child");
        result.NestedMaps["child"].FieldNames.ShouldContain("childField");
    }

    [Fact]
    public void Extract_ClassStyleRecord_ReturnsPropertyNames()
    {
        var result = DtoFieldMap.Extract(typeof(ClassStyleDto));

        result.FieldNames.ShouldBe(new[] { "prop_a", "prop_b" }, ignoreOrder: true);
        result.IsOptional("prop_b").ShouldBeTrue();
    }

    [Fact]
    public void Extract_CachesResults_SameInstanceReturned()
    {
        var result1 = DtoFieldMap.Extract(typeof(SimpleDto));
        var result2 = DtoFieldMap.Extract(typeof(SimpleDto));

        ReferenceEquals(result1, result2).ShouldBeTrue();
    }
}
