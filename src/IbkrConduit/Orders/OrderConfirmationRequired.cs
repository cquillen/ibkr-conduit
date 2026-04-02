using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Orders;

/// <summary>
/// IBKR requires confirmation before proceeding with the order.
/// Caller must decide whether to confirm via <c>ReplyAsync</c>.
/// </summary>
/// <param name="ReplyId">The identifier to pass to ReplyAsync.</param>
/// <param name="Messages">Warning messages from IBKR explaining why confirmation is needed.</param>
/// <param name="MessageIds">IBKR message type identifiers (e.g., "o163", "o354").</param>
[ExcludeFromCodeCoverage]
public sealed record OrderConfirmationRequired(
    string ReplyId,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> MessageIds);
