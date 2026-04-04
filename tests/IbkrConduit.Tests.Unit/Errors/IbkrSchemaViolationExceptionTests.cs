using System.Collections.Generic;
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrSchemaViolationExceptionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var extra = new List<string> { "newField1", "newField2" };
        var missing = new List<string> { "deprecatedField" };

        var ex = new IbkrSchemaViolationException(
            "/v1/api/portfolio/U1234567/summary",
            typeof(string),
            extra,
            missing);

        ex.EndpointPath.ShouldBe("/v1/api/portfolio/U1234567/summary");
        ex.DtoType.ShouldBe(typeof(string));
        ex.ExtraFields.ShouldBe(extra);
        ex.MissingFields.ShouldBe(missing);
        ex.Error.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void Constructor_FormatsMessageWithEndpointAndDtoType()
    {
        var ex = new IbkrSchemaViolationException(
            "/v1/api/test",
            typeof(int),
            ["extra1"],
            ["missing1"]);

        ex.Message.ShouldContain("/v1/api/test");
        ex.Message.ShouldContain("Int32");
    }

    [Fact]
    public void Constructor_EmptyLists_StillSetsProperties()
    {
        var ex = new IbkrSchemaViolationException(
            "/v1/api/test",
            typeof(string),
            [],
            []);

        ex.ExtraFields.ShouldBeEmpty();
        ex.MissingFields.ShouldBeEmpty();
    }
}
