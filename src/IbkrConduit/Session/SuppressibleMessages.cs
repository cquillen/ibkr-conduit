using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Session;

/// <summary>
/// Known IBKR suppressible message IDs for use with
/// <see cref="IbkrClientOptions.SuppressMessageIds"/>.
/// These are passed to POST /iserver/questions/suppress after each session initialization
/// to auto-accept order confirmation prompts.
/// </summary>
/// <remarks>
/// Message IDs prefixed with "o" correspond to TWS API Error Codes.
/// The "p" prefix is for allocation-related warnings.
/// See docs/ibkr-suppressible-message-ids.md for full descriptions.
/// Additional IDs may be discovered at runtime via /iserver/reply responses.
/// </remarks>
[ExcludeFromCodeCoverage]
public static class SuppressibleMessages
{
    /// <summary>The following order exceeds the price percentage limit.</summary>
    public const string PricePercentageConstraint = "o163";

    /// <summary>You are submitting an order without market data.</summary>
    public const string MissingMarketData = "o354";

    /// <summary>The following value exceeds the tick size limit.</summary>
    public const string TickSizeLimit = "o382";

    /// <summary>The following order size exceeds the Size Limit.</summary>
    public const string OrderSizeLimit = "o383";

    /// <summary>This order will most likely trigger and fill immediately.</summary>
    public const string TriggerAndFill = "o403";

    /// <summary>The following order value estimate exceeds the Total Value Limit.</summary>
    public const string OrderValueLimit = "o451";

    /// <summary>Mixed allocation order warning.</summary>
    public const string MixedAllocation = "o2136";

    /// <summary>Cross side order warning.</summary>
    public const string CrossSideOrder = "o2137";

    /// <summary>Instrument does not support trading in fractions outside regular trading hours.</summary>
    public const string FractionsOutsideRth = "o2165";

    /// <summary>Called Bond warning.</summary>
    public const string CalledBond = "o10082";

    /// <summary>The following order size modification exceeds the size modification limit.</summary>
    public const string SizeModificationLimit = "o10138";

    /// <summary>Warns about risks with Market Orders.</summary>
    public const string MarketOrderRisks = "o10151";

    /// <summary>Warns about risks associated with stop orders once they become active.</summary>
    public const string StopOrderRisks = "o10152";

    /// <summary>Confirm Mandatory Cap Price — IB may set a cap/floor to avoid unfair pricing.</summary>
    public const string MandatoryCapPrice = "o10153";

    /// <summary>Cash quantity details are provided on a best efforts basis only.</summary>
    public const string CashQuantity = "o10164";

    /// <summary>Cash Quantity Order Confirmation — monetary value orders are non-guaranteed.</summary>
    public const string CashQuantityOrder = "o10223";

    /// <summary>Warns about risks associated with market orders for Crypto.</summary>
    public const string CryptoMarketOrderRisks = "o10288";

    /// <summary>Stop order type awareness and risk warning.</summary>
    public const string StopOrderTypeRisks = "o10331";

    /// <summary>OSL Digital Securities LTD Crypto Order Warning.</summary>
    public const string OslCryptoOrder = "o10332";

    /// <summary>Option Exercise at the Money warning.</summary>
    public const string OptionExerciseAtm = "o10333";

    /// <summary>Order will be placed into current omnibus account instead of selected global account.</summary>
    public const string OmnibusAccountOrder = "o10334";

    /// <summary>Internal Rapid Entry window.</summary>
    public const string RapidEntry = "o10335";

    /// <summary>This security has limited liquidity — heightened risk.</summary>
    public const string LimitedLiquidity = "o10336";

    /// <summary>Order will be distributed over multiple accounts (allocation warning).</summary>
    public const string MultiAccountAllocation = "p6";

    /// <summary>
    /// Recommended suppressions for automated/algorithmic trading systems.
    /// Covers the most common prompts that would block order flow.
    /// </summary>
    public static readonly IReadOnlyList<string> AutomatedTrading = new[]
    {
        PricePercentageConstraint,
        MissingMarketData,
        TickSizeLimit,
        OrderSizeLimit,
        TriggerAndFill,
        OrderValueLimit,
        SizeModificationLimit,
        MarketOrderRisks,
        StopOrderRisks,
        MandatoryCapPrice,
        StopOrderTypeRisks,
    };
}
