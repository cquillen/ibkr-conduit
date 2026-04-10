using System.Globalization;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Parses raw Flex query XML responses into strongly-typed result records.
/// </summary>
internal static class FlexResultParser
{
    /// <summary>Parses a Cash Transactions Flex query response.</summary>
    public static CashTransactionsFlexResult ParseCashTransactions(XDocument doc)
    {
        var (from, to) = GetDateRange(doc);
        var items = doc.Descendants("CashTransaction").Select(ParseCashTransaction).ToList();
        return new CashTransactionsFlexResult(
            GetQueryName(doc),
            GetGeneratedAt(doc),
            from,
            to,
            items,
            doc);
    }

    /// <summary>Parses a Trade Confirmations Flex query response.</summary>
    public static TradeConfirmationsFlexResult ParseTradeConfirmations(XDocument doc)
    {
        var (from, to) = GetDateRange(doc);
        var trades = doc.Descendants("TradeConfirm").Select(ParseTradeConfirmation).ToList();
        var summaries = doc.Descendants("SymbolSummary").Select(ParseSymbolSummary).ToList();
        var orders = doc.Descendants("Order").Select(ParseOrder).ToList();
        return new TradeConfirmationsFlexResult(
            GetQueryName(doc),
            GetGeneratedAt(doc),
            from,
            to,
            trades,
            summaries,
            orders,
            doc);
    }

    /// <summary>Parses any Flex query response into a generic result with per-statement metadata.</summary>
    public static FlexGenericResult ParseGeneric(XDocument doc) =>
        new(
            GetQueryName(doc),
            GetQueryType(doc),
            GetGeneratedAt(doc),
            ParseStatements(doc),
            doc);

    private static string GetQueryName(XDocument doc) =>
        doc.Root?.Attribute("queryName")?.Value ?? string.Empty;

    private static string GetQueryType(XDocument doc) =>
        doc.Root?.Attribute("type")?.Value ?? string.Empty;

    private static DateTimeOffset? GetGeneratedAt(XDocument doc)
    {
        var first = doc.Descendants("FlexStatement").FirstOrDefault();
        return first is null ? null : ParseFlexDateTime(first.Attribute("whenGenerated")?.Value);
    }

    private static (DateOnly? from, DateOnly? to) GetDateRange(XDocument doc)
    {
        DateOnly? min = null;
        DateOnly? max = null;
        foreach (var stmt in doc.Descendants("FlexStatement"))
        {
            var f = ParseFlexDate(stmt.Attribute("fromDate")?.Value);
            var t = ParseFlexDate(stmt.Attribute("toDate")?.Value);
            if (f is not null && (min is null || f < min))
            {
                min = f;
            }
            if (t is not null && (max is null || t > max))
            {
                max = t;
            }
        }
        return (min, max);
    }

    private static List<FlexStatementInfo> ParseStatements(XDocument doc) =>
        doc.Descendants("FlexStatement")
            .Select(el => new FlexStatementInfo(
                Attr(el, "accountId"),
                ParseFlexDate(el.Attribute("fromDate")?.Value),
                ParseFlexDate(el.Attribute("toDate")?.Value),
                Attr(el, "period"),
                ParseFlexDateTime(el.Attribute("whenGenerated")?.Value),
                el))
            .ToList();

    private static FlexCashTransaction ParseCashTransaction(XElement el) => new()
    {
        AccountId = Attr(el, "accountId"),
        Currency = Attr(el, "currency"),
        FxRateToBase = AttrDecimal(el, "fxRateToBase"),
        AssetCategory = Attr(el, "assetCategory"),
        Symbol = Attr(el, "symbol"),
        Description = Attr(el, "description"),
        Conid = AttrNullableInt(el, "conid"),
        DateTime = ParseFlexDateTime(el.Attribute("dateTime")?.Value),
        SettleDate = ParseFlexDate(el.Attribute("settleDate")?.Value),
        ReportDate = ParseFlexDate(el.Attribute("reportDate")?.Value),
        Amount = AttrDecimal(el, "amount"),
        Type = Attr(el, "type"),
        TransactionId = Attr(el, "transactionID"),
        Code = Attr(el, "code"),
        LevelOfDetail = Attr(el, "levelOfDetail"),
        RawElement = el,
    };

    private static FlexTradeConfirmation ParseTradeConfirmation(XElement el) => new()
    {
        AccountId = Attr(el, "accountId"),
        Currency = Attr(el, "currency"),
        AssetCategory = Attr(el, "assetCategory"),
        SubCategory = Attr(el, "subCategory"),
        Symbol = Attr(el, "symbol"),
        Description = Attr(el, "description"),
        Conid = AttrNullableInt(el, "conid"),
        TradeId = Attr(el, "tradeID"),
        OrderId = Attr(el, "orderID"),
        ExecId = Attr(el, "execID"),
        TradeDate = ParseFlexDate(el.Attribute("tradeDate")?.Value),
        SettleDate = ParseFlexDate(el.Attribute("settleDate")?.Value),
        ReportDate = ParseFlexDate(el.Attribute("reportDate")?.Value),
        OrderTime = ParseFlexDateTime(el.Attribute("orderTime")?.Value),
        DateTime = ParseFlexDateTime(el.Attribute("dateTime")?.Value),
        Exchange = Attr(el, "exchange"),
        BuySell = Attr(el, "buySell"),
        Quantity = AttrDecimal(el, "quantity"),
        Price = AttrDecimal(el, "price"),
        Amount = AttrDecimal(el, "amount"),
        Proceeds = AttrDecimal(el, "proceeds"),
        NetCash = AttrDecimal(el, "netCash"),
        Commission = AttrDecimal(el, "commission"),
        CommissionCurrency = Attr(el, "commissionCurrency"),
        OrderType = Attr(el, "orderType"),
        LevelOfDetail = Attr(el, "levelOfDetail"),
        RawElement = el,
    };

    private static FlexSymbolSummary ParseSymbolSummary(XElement el) => new()
    {
        AccountId = Attr(el, "accountId"),
        Currency = Attr(el, "currency"),
        AssetCategory = Attr(el, "assetCategory"),
        SubCategory = Attr(el, "subCategory"),
        Symbol = Attr(el, "symbol"),
        Description = Attr(el, "description"),
        Conid = AttrNullableInt(el, "conid"),
        ListingExchange = Attr(el, "listingExchange"),
        TradeDate = ParseFlexDate(el.Attribute("tradeDate")?.Value),
        SettleDate = ParseFlexDate(el.Attribute("settleDate")?.Value),
        ReportDate = ParseFlexDate(el.Attribute("reportDate")?.Value),
        BuySell = Attr(el, "buySell"),
        Quantity = AttrDecimal(el, "quantity"),
        Price = AttrDecimal(el, "price"),
        Amount = AttrDecimal(el, "amount"),
        Proceeds = AttrDecimal(el, "proceeds"),
        NetCash = AttrDecimal(el, "netCash"),
        Commission = AttrDecimal(el, "commission"),
        LevelOfDetail = Attr(el, "levelOfDetail"),
        RawElement = el,
    };

    private static FlexOrder ParseOrder(XElement el) => new()
    {
        AccountId = Attr(el, "accountId"),
        Currency = Attr(el, "currency"),
        AssetCategory = Attr(el, "assetCategory"),
        SubCategory = Attr(el, "subCategory"),
        Symbol = Attr(el, "symbol"),
        Description = Attr(el, "description"),
        Conid = AttrNullableInt(el, "conid"),
        OrderId = Attr(el, "orderID"),
        OrderTime = ParseFlexDateTime(el.Attribute("orderTime")?.Value),
        TradeDate = ParseFlexDate(el.Attribute("tradeDate")?.Value),
        SettleDate = ParseFlexDate(el.Attribute("settleDate")?.Value),
        ReportDate = ParseFlexDate(el.Attribute("reportDate")?.Value),
        Exchange = Attr(el, "exchange"),
        BuySell = Attr(el, "buySell"),
        Quantity = AttrDecimal(el, "quantity"),
        Price = AttrDecimal(el, "price"),
        Amount = AttrDecimal(el, "amount"),
        Proceeds = AttrDecimal(el, "proceeds"),
        NetCash = AttrDecimal(el, "netCash"),
        Commission = AttrDecimal(el, "commission"),
        OrderType = Attr(el, "orderType"),
        LevelOfDetail = Attr(el, "levelOfDetail"),
        RawElement = el,
    };

    /// <summary>Parses a Flex date attribute. Accepts yyyyMMdd or yyyy-MM-dd.</summary>
    internal static DateOnly? ParseFlexDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        if (DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
        {
            return d;
        }
        return null;
    }

    /// <summary>Parses a Flex datetime attribute. Accepts yyyyMMdd;HHmmss or yyyy-MM-dd;HH:mm:ss TZ.</summary>
    internal static DateTimeOffset? ParseFlexDateTime(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        if (DateTimeOffset.TryParseExact(value, "yyyyMMdd;HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
        {
            return dt;
        }
        // Flex also uses bare dates in some fields (e.g. "20260304").
        if (DateTimeOffset.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt))
        {
            return dt;
        }
        // Formats like "2026-04-09;21:23:54 EDT" — replace ; with space and let the general parser handle it.
        var normalized = value.Replace(";", " ");
        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return dt;
        }
        // .NET on non-Windows platforms does not recognize US tz abbreviations like EDT/EST.
        // Strip a trailing abbreviation and apply a known offset manually.
        var lastSpace = normalized.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            var tz = normalized[(lastSpace + 1)..];
            var timestamp = normalized[..lastSpace];
            var offset = tz switch
            {
                "EDT" => TimeSpan.FromHours(-4),
                "EST" => TimeSpan.FromHours(-5),
                "CDT" => TimeSpan.FromHours(-5),
                "CST" => TimeSpan.FromHours(-6),
                "MDT" => TimeSpan.FromHours(-6),
                "MST" => TimeSpan.FromHours(-7),
                "PDT" => TimeSpan.FromHours(-7),
                "PST" => TimeSpan.FromHours(-8),
                "UTC" or "GMT" => TimeSpan.Zero,
                _ => (TimeSpan?)null,
            };
            if (offset is not null && DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var naive))
            {
                return new DateTimeOffset(DateTime.SpecifyKind(naive, DateTimeKind.Unspecified), offset.Value);
            }
        }
        return null;
    }

    private static string Attr(XElement el, string name) =>
        el.Attribute(name)?.Value ?? string.Empty;

    private static int? AttrNullableInt(XElement el, string name) =>
        int.TryParse(el.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    private static decimal AttrDecimal(XElement el, string name) =>
        decimal.TryParse(el.Attribute(name)?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
