using System.Globalization;
using System.Xml.Linq;

namespace IbkrConduit.Flex;

/// <summary>
/// Parses raw Flex query XML responses into strongly-typed result records.
/// </summary>
internal static class FlexResultParser
{
    /// <summary>Parses a Cash Transactions Flex query response.</summary>
    /// <param name="doc">The raw Flex query XML.</param>
    /// <param name="onMoneyParseFailure">Optional sink invoked as <c>(fieldName, rawValue)</c> for every
    /// present-but-unparseable money/quantity attribute (§11.10). The value is surfaced as <c>null</c>
    /// regardless; this signal makes the parse failure observable without altering the DTO.</param>
    public static CashTransactionsFlexResult ParseCashTransactions(
        XDocument doc, Action<string, string>? onMoneyParseFailure = null)
    {
        var (from, to) = GetDateRange(doc);
        var items = doc.Descendants("CashTransaction")
            .Select(el => ParseCashTransaction(el, onMoneyParseFailure)).ToList();
        return new CashTransactionsFlexResult(
            GetQueryName(doc),
            GetGeneratedAt(doc),
            from,
            to,
            items,
            doc);
    }

    /// <summary>Parses a Trade Confirmations Flex query response.</summary>
    /// <param name="doc">The raw Flex query XML.</param>
    /// <param name="onMoneyParseFailure">Optional sink invoked as <c>(fieldName, rawValue)</c> for every
    /// present-but-unparseable money/quantity attribute (§11.10). The value is surfaced as <c>null</c>
    /// regardless; this signal makes the parse failure observable without altering the DTO.</param>
    public static TradeConfirmationsFlexResult ParseTradeConfirmations(
        XDocument doc, Action<string, string>? onMoneyParseFailure = null)
    {
        var (from, to) = GetDateRange(doc);
        var trades = doc.Descendants("TradeConfirm")
            .Select(el => ParseTradeConfirmation(el, onMoneyParseFailure)).ToList();
        var summaries = doc.Descendants("SymbolSummary")
            .Select(el => ParseSymbolSummary(el, onMoneyParseFailure)).ToList();
        var orders = doc.Descendants("Order")
            .Select(el => ParseOrder(el, onMoneyParseFailure)).ToList();
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

    private static FlexCashTransaction ParseCashTransaction(XElement el, Action<string, string>? onParseFailure) => new()
    {
        AccountId = Attr(el, "accountId"),
        Currency = Attr(el, "currency"),
        FxRateToBase = AttrNullableDecimal(el, "fxRateToBase", onParseFailure),
        AssetCategory = Attr(el, "assetCategory"),
        Symbol = Attr(el, "symbol"),
        Description = Attr(el, "description"),
        Conid = AttrNullableInt(el, "conid"),
        DateTime = ParseFlexDateTime(el.Attribute("dateTime")?.Value),
        SettleDate = ParseFlexDate(el.Attribute("settleDate")?.Value),
        ReportDate = ParseFlexDate(el.Attribute("reportDate")?.Value),
        Amount = AttrNullableDecimal(el, "amount", onParseFailure),
        Type = Attr(el, "type"),
        TransactionId = Attr(el, "transactionID"),
        Code = Attr(el, "code"),
        LevelOfDetail = Attr(el, "levelOfDetail"),
        RawElement = el,
    };

    private static FlexTradeConfirmation ParseTradeConfirmation(XElement el, Action<string, string>? onParseFailure) => new()
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
        Quantity = AttrNullableDecimal(el, "quantity", onParseFailure),
        Price = AttrNullableDecimal(el, "price", onParseFailure),
        Amount = AttrNullableDecimal(el, "amount", onParseFailure),
        Proceeds = AttrNullableDecimal(el, "proceeds", onParseFailure),
        NetCash = AttrNullableDecimal(el, "netCash", onParseFailure),
        Commission = AttrNullableDecimal(el, "commission", onParseFailure),
        CommissionCurrency = Attr(el, "commissionCurrency"),
        OrderType = Attr(el, "orderType"),
        LevelOfDetail = Attr(el, "levelOfDetail"),
        RawElement = el,
    };

    private static FlexSymbolSummary ParseSymbolSummary(XElement el, Action<string, string>? onParseFailure) => new()
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
        Quantity = AttrNullableDecimal(el, "quantity", onParseFailure),
        Price = AttrNullableDecimal(el, "price", onParseFailure),
        Amount = AttrNullableDecimal(el, "amount", onParseFailure),
        Proceeds = AttrNullableDecimal(el, "proceeds", onParseFailure),
        NetCash = AttrNullableDecimal(el, "netCash", onParseFailure),
        Commission = AttrNullableDecimal(el, "commission", onParseFailure),
        LevelOfDetail = Attr(el, "levelOfDetail"),
        RawElement = el,
    };

    private static FlexOrder ParseOrder(XElement el, Action<string, string>? onParseFailure) => new()
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
        Quantity = AttrNullableDecimal(el, "quantity", onParseFailure),
        Price = AttrNullableDecimal(el, "price", onParseFailure),
        Amount = AttrNullableDecimal(el, "amount", onParseFailure),
        Proceeds = AttrNullableDecimal(el, "proceeds", onParseFailure),
        NetCash = AttrNullableDecimal(el, "netCash", onParseFailure),
        Commission = AttrNullableDecimal(el, "commission", onParseFailure),
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

    /// <summary>
    /// Best-effort parse of a Flex datetime attribute (§11.10, RST-3). Accepts the compact
    /// <c>yyyyMMdd;HHmmss</c> form, a bare <c>yyyyMMdd</c> date, and ISO-ish forms carrying an
    /// explicit numeric UTC offset. A timestamp that carries only a timezone <em>abbreviation</em>
    /// (e.g. <c>EDT</c>, <c>CET</c>, <c>BST</c>, <c>HKT</c>) returns <see langword="null"/>: the
    /// parser never guesses a UTC offset from an abbreviation, so it never fabricates a
    /// wrong-or-inconsistent offset. Callers recover the raw wire string from the row's
    /// <c>RawElement</c>, so a null parse is never a silent data loss.
    /// </summary>
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
        // Forms like "2026-04-09;21:23:54-04:00" — normalize the ';' separator and let the general
        // parser handle an explicit numeric offset. Timezone abbreviations are deliberately NOT
        // mapped to an offset (RST-3): if the general parser can't resolve it, the result is null
        // and the raw string is preserved on the caller's RawElement.
        var normalized = value.Replace(";", " ");
        if (DateTimeOffset.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return dt;
        }
        return null;
    }

    private static string Attr(XElement el, string name) =>
        el.Attribute(name)?.Value ?? string.Empty;

    private static int? AttrNullableInt(XElement el, string name) =>
        int.TryParse(el.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

    /// <summary>
    /// Parses a money/quantity attribute with wire fidelity (§11.10, RST-1). An absent or
    /// present-but-empty attribute yields <see langword="null"/> silently (the empty-money wire
    /// convention). A present, non-empty value that does not parse also yields <see langword="null"/>
    /// — never a fabricated <c>0</c> — and additionally invokes <paramref name="onParseFailure"/>
    /// with the field name and the raw wire text so the failure is observable. The raw text also
    /// remains recoverable from the row's <c>RawElement</c>.
    /// </summary>
    private static decimal? AttrNullableDecimal(XElement el, string name, Action<string, string>? onParseFailure)
    {
        var raw = el.Attribute(name)?.Value;
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }
        onParseFailure?.Invoke(name, raw);
        return null;
    }
}
