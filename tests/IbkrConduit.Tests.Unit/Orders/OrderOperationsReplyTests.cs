using System;
using System.Collections.Generic;
using IbkrConduit.Client;
using IbkrConduit.Orders;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

/// <summary>
/// Tests for <see cref="OrderOperations.DeserializeReplyResponse"/> which handles
/// IBKR's inconsistent reply endpoint responses (array vs bare object).
/// </summary>
public class OrderOperationsReplyTests
{
    [Fact]
    public void DeserializeReplyResponse_ArrayResponse_ReturnsListDirectly()
    {
        var json = """
            [
                {
                    "order_id": "12345",
                    "order_status": "Submitted"
                }
            ]
            """;

        var result = OrderOperations.DeserializeReplyResponse(json);

        result.Count.ShouldBe(1);
        result[0].OrderId.ShouldBe("12345");
        result[0].OrderStatus.ShouldBe("Submitted");
    }

    [Fact]
    public void DeserializeReplyResponse_BareObjectResponse_WrapsInList()
    {
        var json = """
            {
                "order_id": "67890",
                "order_status": "Submitted"
            }
            """;

        var result = OrderOperations.DeserializeReplyResponse(json);

        result.Count.ShouldBe(1);
        result[0].OrderId.ShouldBe("67890");
        result[0].OrderStatus.ShouldBe("Submitted");
    }

    [Fact]
    public void DeserializeReplyResponse_BareObjectWithConfirmed_WrapsInList()
    {
        var json = """{"confirmed":true}""";

        var result = OrderOperations.DeserializeReplyResponse(json);

        result.Count.ShouldBe(1);
        // confirmed is not a mapped field, so all named properties will be null
        result[0].OrderId.ShouldBeNull();
    }

    [Fact]
    public void DeserializeReplyResponse_MultipleItemsArray_ReturnsAll()
    {
        var json = """
            [
                {
                    "order_id": "111",
                    "order_status": "Submitted"
                },
                {
                    "order_id": "222",
                    "order_status": "PreSubmitted"
                }
            ]
            """;

        var result = OrderOperations.DeserializeReplyResponse(json);

        result.Count.ShouldBe(2);
        result[0].OrderId.ShouldBe("111");
        result[1].OrderId.ShouldBe("222");
    }

    [Fact]
    public void DeserializeReplyResponse_QuestionResponse_ParsesCorrectly()
    {
        var json = """
            {
                "id": "reply-abc",
                "message": ["Are you sure?"],
                "isSuppressed": false,
                "messageIds": ["o123"]
            }
            """;

        var result = OrderOperations.DeserializeReplyResponse(json);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("reply-abc");
        result[0].Message.ShouldNotBeNull();
        result[0].Message!.Count.ShouldBe(1);
    }

    [Fact]
    public void DeserializeReplyResponse_EmptyString_ThrowsInvalidOperationException()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => OrderOperations.DeserializeReplyResponse(""));

        ex.Message.ShouldContain("empty");
    }

    [Fact]
    public void DeserializeReplyResponse_WhitespaceOnly_ThrowsInvalidOperationException()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => OrderOperations.DeserializeReplyResponse("   "));

        ex.Message.ShouldContain("empty");
    }

    [Fact]
    public void DeserializeReplyResponse_InvalidJson_ThrowsInvalidOperationException()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => OrderOperations.DeserializeReplyResponse("not-json"));

        ex.Message.ShouldContain("unexpected content");
    }
}
