using System.Collections.Generic;
using IbkrConduit.Client;
using IbkrConduit.Errors;
using IbkrConduit.Orders;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Orders;

/// <summary>
/// Tests for <see cref="OrderOperations.ClassifyGroupResponses"/> — the ADR-0008 §9.11 per-leg outcome
/// classifier used only by <c>PlaceOrdersAsync</c>. Every case keys on each row's field signature, never
/// on array position. Fixtures reproduce the exact wire shapes the 2026-07-14 live probe recorded
/// (<c>recordings/rpd02-invalidchild-*</c>, gitignored — PII); the shapes are documented verbatim in the
/// RPD-03 spec and ADR-0008.
/// </summary>
public class ClassifyGroupResponsesTests
{
    private const string _rawBody = "[raw wire body]";
    private const string _requestPath = "/v1/api/iserver/account/DU1234567/orders";

    private static OrderRequest Order(string? coid = null, string? parentId = null) =>
        new()
        {
            Conid = 265598,
            Side = "BUY",
            Quantity = 1,
            OrderType = "LMT",
            Price = 1.00m,
            Tif = "GTC",
            CustomerOrderId = coid,
            ParentId = parentId,
        };

    private static OrderSubmissionResponse Leg(
        string? orderId = null, string? orderStatus = null, string? localOrderId = null,
        string? ocaGroupId = null, string? id = null, List<string>? message = null, string? error = null) =>
        new(id, message, null, null, orderId, orderStatus, localOrderId, ocaGroupId) { Error = error };

    [Fact]
    public void ClassifyGroupResponses_CleanTwoLegBracket_ReturnsBothOrderSubmitted()
    {
        // Both legs real order_id + non-terminal order_status — a fully-transmitted bracket. Child first
        // (parent_order_id linkage), parent second (local_order_id), matching the probe's array ordering —
        // but classification keys on field signature, not position.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1760268468", orderStatus: "PreSubmitted"),
            Leg(orderId: "1760268467", orderStatus: "PreSubmitted", localOrderId: "Parent"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(2);
        legs[0].IsT0.ShouldBeTrue();
        legs[0].AsT0.OrderId.ShouldBe("1760268468");
        legs[1].IsT0.ShouldBeTrue();
        legs[1].AsT0.OrderId.ShouldBe("1760268467");
        legs[1].AsT0.LocalOrderId.ShouldBe("Parent");
    }

    [Fact]
    public void ClassifyGroupResponses_SentinelChildAndInactiveParent_ClassifiesBothAsRejected()
    {
        // THE money-safety regression (ADR-0008): the dangerous case. Child is a sentinel reject
        // (order_id="-1", Failed); the parent carries a REAL-looking order_id but a terminal "Inactive"
        // status (whole bracket rejected together). Neither may surface as OrderSubmitted — the parent's
        // real order_id must NOT fool the classifier. Without the terminal-status check the second element
        // would classify OrderSubmitted (this is the false-green guard).
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "-1", orderStatus: "Failed"),
            Leg(orderId: "1760268467", orderStatus: "Inactive"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(2);
        legs[0].IsT1.ShouldBeTrue("the order_id=-1/Failed sentinel child must be a rejection");
        legs[1].IsT1.ShouldBeTrue("the real-order_id/Inactive parent must be a rejection, not OrderSubmitted");
        legs[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        legs[1].AsT1.RejectionMessage.ShouldContain("Inactive");
        legs[1].AsT1.RawBody.ShouldBe(_rawBody);
    }

    [Fact]
    public void ClassifyGroupResponses_RealIdArrayFromConfirmationChain_ReturnsBothOrderSubmitted()
    {
        // Real order_id on both legs after a confirmation-question chain: one PendingSubmit (child), one
        // PreSubmitted (parent, local_order_id). No false-positive rejection on legitimate non-terminal
        // statuses like PendingSubmit/PreSubmitted.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1799911", orderStatus: "PendingSubmit"),
            Leg(orderId: "1799910", orderStatus: "PreSubmitted", localOrderId: "Parent"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(2);
        legs[0].IsT0.ShouldBeTrue();
        legs[0].AsT0.OrderStatus.ShouldBe("PendingSubmit");
        legs[1].IsT0.ShouldBeTrue();
        legs[1].AsT0.OrderStatus.ShouldBe("PreSubmitted");
    }

    [Fact]
    public void ClassifyGroupResponses_EmptyArray_ReturnsWholeCallRejection()
    {
        // Regression guard: an empty array still yields the existing whole-call IbkrOrderRejectedError
        // (AMB-4) — unchanged by per-leg classification.
        var responses = new List<OrderSubmissionResponse>();
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeFalse();
        var rejected = result.Error.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RawBody.ShouldBe(_rawBody);
    }

    [Fact]
    public void ClassifyGroupResponses_SingleQuestion_ReturnsConfirmationForWholeGroup()
    {
        // Regression guard: a single question-shaped element still produces one OrderConfirmationRequired
        // for the whole group — a question blocks every leg alike, nothing to break down per-leg yet.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(id: "reply-1", message: new List<string> { "Confirm?" }),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsT1.ShouldBeTrue();
        result.Value.AsT1.ReplyId.ShouldBe("reply-1");
    }

    [Fact]
    public void ClassifyGroupResponses_LegCountShortfall_AppendsAmbiguousForMissingLeg()
    {
        // Defensible generalization, NOT wire-observed (only 2-leg groups were probed): 3 requested orders
        // (1 parent + 2 children), response array holds only 2 entries. The 2 real legs classify normally;
        // one trailing IbkrAmbiguousOrderError stands in for the leg missing from the response.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1799911", orderStatus: "PreSubmitted"),
            Leg(orderId: "1799910", orderStatus: "PreSubmitted", localOrderId: "Parent"),
        };
        var orders = new List<OrderRequest>
        {
            Order(coid: "Parent"), Order(parentId: "Parent"), Order(parentId: "Parent"),
        };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(3);
        legs[0].IsT0.ShouldBeTrue();
        legs[1].IsT0.ShouldBeTrue();
        legs[2].IsT2.ShouldBeTrue("the leg missing from the response must be ambiguous, never dropped");
        legs[2].AsT2.ShouldBeOfType<IbkrAmbiguousOrderError>();
    }

    [Fact]
    public void ClassifyGroupResponses_UnrecognizedLegShape_ReturnsAmbiguousNotThrow()
    {
        // Defensive fallback: an element with no error, no recognized status, and no order_id matches
        // nothing above. It degrades to IbkrAmbiguousOrderError — never an exception, never a silent
        // OrderSubmitted with empty fields.
        var responses = new List<OrderSubmissionResponse> { Leg() };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(1);
        legs[0].IsT2.ShouldBeTrue();
        legs[0].AsT2.ShouldBeOfType<IbkrAmbiguousOrderError>();
    }

    [Fact]
    public void ClassifyGroupResponses_ArrayWrappedErrorLeg_ClassifiesThatLegAsRejected()
    {
        // A leg carrying an explicit error field is a definite per-leg rejection (an array-wrapped
        // [{"error":"…"}] element bypasses bare-object hidden-error detection).
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(error: "We cannot accept an order at the limit price you selected."),
            Leg(orderId: "1760268467", orderStatus: "PreSubmitted", localOrderId: "Parent"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs[0].IsT1.ShouldBeTrue();
        legs[0].AsT1.RejectionMessage.ShouldContain("cannot accept an order");
        legs[1].IsT0.ShouldBeTrue();
    }
}
