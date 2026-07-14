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
        string? ocaGroupId = null, string? id = null, List<string>? message = null, string? error = null,
        string? text = null, string? warningMessage = null, List<string>? messageOptions = null,
        string? parentOrderId = null) =>
        new(id, message, null, null, orderId, orderStatus, localOrderId, ocaGroupId)
        {
            Error = error,
            Text = text,
            WarningMessage = warningMessage,
            MessageOptions = messageOptions,
            ParentOrderId = parentOrderId,
        };

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
    public void ClassifyGroupResponses_CaseVariantTerminalStatus_ClassifiesAsRejected()
    {
        // Locks the OrdinalIgnoreCase contract on the terminal-non-transmitting set: a case variant of a
        // known terminal wire value ("inactive"/"FAILED") must still be a definite rejection, never fall
        // through to a false OrderSubmitted. Without the case-insensitive comparer these rows would skip the
        // rejection branch and (pre-fix) surface as OrderSubmitted for a leg that never transmitted.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1760268467", orderStatus: "FAILED"),
            Leg(orderId: "1760268468", orderStatus: "inactive"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(2);
        legs[0].IsT1.ShouldBeTrue("a case-variant \"FAILED\" terminal status must be a rejection, not OrderSubmitted");
        legs[1].IsT1.ShouldBeTrue("a case-variant \"inactive\" terminal status must be a rejection, not OrderSubmitted");
        legs[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        legs[1].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
    }

    [Fact]
    public void ClassifyGroupResponses_RealOrderIdUnrecognizedTerminalStatus_ClassifiesAmbiguousNotSubmitted()
    {
        // THE core money-safety fix (ADR-0008): a leg with a REAL order_id under a terminal/non-transmitting
        // order_status OUTSIDE the small known-terminal set ("Cancelled", "Rejected", "PendingCancel", …) must
        // NOT surface as OrderSubmitted. The positive transmitting allowlist fails this toward the safe
        // direction — IbkrAmbiguousOrderError — because IBKR's status vocabulary is not closed. The pre-fix
        // blocklist gate let exactly this shape fall through to OrderSubmitted (the false-green this story kills).
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1760268467", orderStatus: "Cancelled"),
            Leg(orderId: "1760268468", orderStatus: "Rejected"),
            Leg(orderId: "1760268469", orderStatus: "PendingCancel"),
        };
        var orders = new List<OrderRequest>
        {
            Order(coid: "Parent"), Order(parentId: "Parent"), Order(parentId: "Parent"),
        };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(3);
        legs[0].IsT0.ShouldBeFalse("a real order_id under an unrecognized terminal status must NOT be OrderSubmitted");
        legs[0].IsT2.ShouldBeTrue("an unrecognized terminal status degrades to ambiguous, the safe direction");
        legs[1].IsT2.ShouldBeTrue("\"Rejected\" is not a recognized transmitting status — ambiguous, not OrderSubmitted");
        legs[2].IsT2.ShouldBeTrue("\"PendingCancel\" is not a recognized transmitting status — ambiguous");
        legs[0].AsT2.ShouldBeOfType<IbkrAmbiguousOrderError>();
    }

    [Fact]
    public void ClassifyGroupResponses_SentinelOrderIdWithLiveStatus_ClassifiesAmbiguousNotSubmitted()
    {
        // Real-vs-sentinel order_id signal (design doc §9.11): a non-positive sentinel order_id ("-1") paired
        // with a status that WOULD be transmitting on a real id must still not surface as OrderSubmitted —
        // "-1" is never a transmitted order identity. Degrades to ambiguous, never a false OrderSubmitted.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "-1", orderStatus: "PreSubmitted"),
            Leg(orderId: "0", orderStatus: "Submitted"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(2);
        legs[0].IsT0.ShouldBeFalse("a sentinel order_id (\"-1\") must never surface as OrderSubmitted");
        legs[0].IsT2.ShouldBeTrue("a sentinel order_id degrades to ambiguous");
        legs[1].IsT0.ShouldBeFalse("a non-positive order_id (\"0\") must never surface as OrderSubmitted");
        legs[1].IsT2.ShouldBeTrue("a non-positive order_id degrades to ambiguous");
    }

    [Fact]
    public void ClassifyGroupResponses_CaseVariantTransmittingStatus_ReturnsOrderSubmitted()
    {
        // Locks OrdinalIgnoreCase on the positive transmitting allowlist: a case variant of a live wire status
        // ("presubmitted") on a real order_id still classifies as a transmitted OrderSubmitted — the guard is
        // case-insensitive in both directions so a wire-case wobble neither fabricates nor suppresses success.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1760268467", orderStatus: "presubmitted"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var legs = result.Value.AsT0;
        legs.Count.ShouldBe(1);
        legs[0].IsT0.ShouldBeTrue("a case-variant transmitting status on a real order_id is OrderSubmitted");
        legs[0].AsT0.OrderId.ShouldBe("1760268467");
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

    // --- RPD-04: text / warning_message / messageOptions / parent_order_id typed exposure ---

    [Fact]
    public void ClassifyGroupResponses_TerminalStatusLegWithText_MessageEnrichedFromText()
    {
        // RPD-04 Done when: a leg classified via RPD-03's terminal-status path enriches its message from
        // `text` (documented + observed on advancedOrderReject, DOC-01) instead of the generic
        // "Order not transmitted (status: ...)" fallback. Shape matches the sentinel row observed by the
        // 2026-07-14 probe (recordings/rpd02-invalidchild-b-negprice, gitignored — PII).
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "-1", orderStatus: "Failed", text: "The size, 1, does not conform to the minimum variation of 100 for this contract."),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var rejected = result.Value.AsT0[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldBe("The size, 1, does not conform to the minimum variation of 100 for this contract.");
        rejected.Text.ShouldBe("The size, 1, does not conform to the minimum variation of 100 for this contract.");
    }

    [Fact]
    public void ClassifyGroupResponses_TerminalStatusLegWithWarningMessageOnly_MessageEnrichedFromWarningMessage()
    {
        // `warning_message` is observed on the wire in the orders context (undocumented there — the same
        // field name is documented elsewhere as an FYI/alert field that always returns null, a distinct
        // context). When `text` is absent it still enriches the rejection message.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "-1", orderStatus: "Failed", warningMessage: "Order rejected by the exchange."),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var rejected = result.Value.AsT0[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldBe("Order rejected by the exchange.");
        rejected.WarningMessage.ShouldBe("Order rejected by the exchange.");
        rejected.Text.ShouldBeNull();
    }

    [Fact]
    public void ClassifyGroupResponses_TerminalStatusLegWithTextAndWarningMessage_PrefersTextOverWarningMessage()
    {
        // Both fields observed together on the same sentinel row (the probe's negprice sample) — `text` is
        // the documented field, so it wins the message-enrichment precedence, while both stay independently
        // exposed as typed fields (never discarded).
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "-1", orderStatus: "Failed", text: "Documented reject text.", warningMessage: "Undocumented warning text."),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var rejected = result.Value.AsT0[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldBe("Documented reject text.");
        rejected.Text.ShouldBe("Documented reject text.");
        rejected.WarningMessage.ShouldBe("Undocumented warning text.");
    }

    [Fact]
    public void ClassifyGroupResponses_TerminalStatusLegWithEmptyText_FallsBackToWarningMessage()
    {
        // Presence-not-emptiness guard: an empty string `text:""` on the wire must not win the
        // enrichment precedence over a populated `warning_message` — null-coalescing alone would treat
        // "" as present and produce a blank RejectionMessage.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "-1", orderStatus: "Failed", text: string.Empty, warningMessage: "Order rejected by the exchange."),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var rejected = result.Value.AsT0[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldBe("Order rejected by the exchange.");
        rejected.Text.ShouldBe(string.Empty);
        rejected.WarningMessage.ShouldBe("Order rejected by the exchange.");
    }

    [Fact]
    public void ClassifyGroupResponses_TerminalStatusLegWithEmptyTextAndEmptyWarningMessage_UsesGenericFallbackMessage()
    {
        // Both wire fields present but empty must fall all the way through to the generic status-only
        // fallback message, not produce a blank RejectionMessage.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "-1", orderStatus: "Failed", text: string.Empty, warningMessage: string.Empty),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var rejected = result.Value.AsT0[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldBe("Order not transmitted (status: Failed).");
        rejected.Text.ShouldBe(string.Empty);
        rejected.WarningMessage.ShouldBe(string.Empty);
    }

    [Fact]
    public void ClassifyGroupResponses_TerminalStatusLegWithNeitherTextNorWarning_UsesGenericFallbackMessage()
    {
        // False-green guard: this is the pre-RPD-04 shape (no text/warning_message on the wire row) —
        // the generic fallback must still be used, and the new typed fields must be null, not fabricated.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1760268467", orderStatus: "Inactive"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var rejected = result.Value.AsT0[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldBe("Order not transmitted (status: Inactive).");
        rejected.Text.ShouldBeNull();
        rejected.WarningMessage.ShouldBeNull();
    }

    [Fact]
    public void ClassifyGroupResponses_ErrorLeg_ExposesTextAndWarningMessageTypedFields()
    {
        // The explicit-error branch (a leg carrying `error`) also exposes text/warning_message as typed
        // fields when present, even though the rejection message itself comes from `error`.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(error: "Rejected.", text: "Extra reject detail.", warningMessage: "Extra warning detail."),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var rejected = result.Value.AsT0[0].AsT1.ShouldBeOfType<IbkrOrderRejectedError>();
        rejected.RejectionMessage.ShouldBe("Rejected.");
        rejected.Text.ShouldBe("Extra reject detail.");
        rejected.WarningMessage.ShouldBe("Extra warning detail.");
    }

    [Fact]
    public void ClassifyGroupResponses_TransmittedLegWithParentOrderId_ExposesParentOrderIdOnOrderSubmitted()
    {
        // `parent_order_id` — observed on a child's submission-response row in the confirmation-chain
        // scenario (recordings/rpd02-invalidchild-c-mismatchconid, gitignored — PII), linking it to the
        // parent's order_id. Distinct from the request-side ParentId/cOID and from LiveOrder.ParentId (RPD-02).
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1799911", orderStatus: "PendingSubmit", parentOrderId: "1799910"),
        };
        var orders = new List<OrderRequest> { Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var submitted = result.Value.AsT0[0].AsT0;
        submitted.OrderId.ShouldBe("1799911");
        submitted.ParentOrderId.ShouldBe("1799910");
    }

    [Fact]
    public void ClassifyGroupResponses_TransmittedLegWithoutParentOrderId_ParentOrderIdIsNull()
    {
        // Regression guard: a parent leg (no parent_order_id on the wire) must not fabricate one.
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(orderId: "1799910", orderStatus: "PreSubmitted", localOrderId: "Parent"),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        var submitted = result.Value.AsT0[0].AsT0;
        submitted.ParentOrderId.ShouldBeNull();
    }

    [Fact]
    public void ClassifyGroupResponses_SingleQuestionWithMessageOptions_ExposesMessageOptionsOnConfirmation()
    {
        // `messageOptions` — documented only in a DOC-03 worked example (not in DOC-03's own field-list
        // prose, absent from DOC-01's formal schema); observed on the question row of the confirmation-chain
        // probe (recordings/rpd02-invalidchild-c-mismatchconid, gitignored — PII).
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(id: "reply-1", message: new List<string> { "Confirm?" }, messageOptions: new List<string> { "Yes", "No" }),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsT1.ShouldBeTrue();
        result.Value.AsT1.MessageOptions.ShouldNotBeNull();
        result.Value.AsT1.MessageOptions.ShouldBe(["Yes", "No"]);
    }

    [Fact]
    public void ClassifyGroupResponses_SingleQuestionWithoutMessageOptions_MessageOptionsIsNull()
    {
        // Regression guard: absence on the wire must surface as null, never a fabricated empty list
        // (ADR-0001 nullable-as-presence).
        var responses = new List<OrderSubmissionResponse>
        {
            Leg(id: "reply-1", message: new List<string> { "Confirm?" }),
        };
        var orders = new List<OrderRequest> { Order(coid: "Parent"), Order(parentId: "Parent") };

        var result = OrderOperations.ClassifyGroupResponses(responses, orders, _rawBody, _requestPath);

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsT1.ShouldBeTrue();
        result.Value.AsT1.MessageOptions.ShouldBeNull();
    }
}
