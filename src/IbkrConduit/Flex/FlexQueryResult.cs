using System.Globalization;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Wraps a Flex Query XML response and provides typed access to common sections.
/// </summary>
public class FlexQueryResult
{
    /// <summary>The full XML document returned by the Flex Web Service.</summary>
    public XDocument RawXml { get; }

    /// <summary>
    /// Typed trade records from the Trades or TradeConfirmations section, if present.
    /// Returns empty list if the section is not in the query template.
    /// </summary>
    public IReadOnlyList<FlexTrade> Trades { get; }

    /// <summary>
    /// Typed open position records from the OpenPositions section, if present.
    /// Returns empty list if the section is not in the query template.
    /// </summary>
    public IReadOnlyList<FlexPosition> OpenPositions { get; }

    /// <summary>
    /// Creates a new <see cref="FlexQueryResult"/> by parsing the given XML document.
    /// </summary>
    /// <param name="rawXml">The raw XML document from the Flex Web Service.</param>
    public FlexQueryResult(XDocument rawXml)
    {
        RawXml = rawXml;
        Trades = ParseTrades(rawXml);
        OpenPositions = ParsePositions(rawXml);
    }

    private static List<FlexTrade> ParseTrades(XDocument doc)
    {
        var trades = new List<FlexTrade>();

        foreach (var statement in doc.Descendants("FlexStatement"))
        {
            // Look for <Trades><Trade .../> elements
            foreach (var element in statement.Descendants("Trade"))
            {
                trades.Add(MapTrade(element));
            }

            // Also look for <TradeConfirmations><TradeConfirmation .../> elements
            foreach (var element in statement.Descendants("TradeConfirmation"))
            {
                trades.Add(MapTrade(element));
            }
        }

        return trades;
    }

    private static List<FlexPosition> ParsePositions(XDocument doc)
    {
        var positions = new List<FlexPosition>();

        foreach (var statement in doc.Descendants("FlexStatement"))
        {
            foreach (var element in statement.Descendants("OpenPosition"))
            {
                positions.Add(MapPosition(element));
            }
        }

        return positions;
    }

    private static FlexTrade MapTrade(XElement element) =>
        new()
        {
            AccountId = Attr(element, "accountId"),
            Symbol = Attr(element, "symbol"),
            Conid = ParseNullableInt(element, "conid"),
            Description = Attr(element, "description"),
            Side = Attr(element, "buySell"),
            Quantity = ParseDecimal(element, "quantity"),
            Price = ParseDecimal(element, "price"),
            Proceeds = ParseDecimal(element, "proceeds"),
            Commission = ParseDecimal(element, "commission"),
            Currency = Attr(element, "currency"),
            TradeDate = Attr(element, "tradeDate"),
            TradeTime = Attr(element, "tradeTime"),
            OrderType = Attr(element, "orderType"),
            Exchange = Attr(element, "exchange"),
            OrderId = Attr(element, "orderId"),
            ExecId = Attr(element, "execId"),
            RawElement = element,
        };

    private static FlexPosition MapPosition(XElement element) =>
        new()
        {
            AccountId = Attr(element, "accountId"),
            Symbol = Attr(element, "symbol"),
            Conid = ParseNullableInt(element, "conid"),
            Description = Attr(element, "description"),
            Position = ParseDecimal(element, "position"),
            MarkPrice = ParseDecimal(element, "markPrice"),
            PositionValue = ParseDecimal(element, "positionValue"),
            CostBasis = ParseDecimal(element, "costBasisMoney"),
            UnrealizedPnl = ParseDecimal(element, "fifoPnlUnrealized"),
            Currency = Attr(element, "currency"),
            AssetClass = Attr(element, "assetCategory"),
            RawElement = element,
        };

    private static string Attr(XElement element, string name) =>
        element.Attribute(name)?.Value ?? string.Empty;

    private static int? ParseNullableInt(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }

    private static decimal ParseDecimal(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value;
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0m;
    }
}
