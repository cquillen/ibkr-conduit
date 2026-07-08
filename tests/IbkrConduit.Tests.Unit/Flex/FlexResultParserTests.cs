using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using IbkrConduit.Flex;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexResultParserTests
{
    private static XDocument LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Flex", "Fixtures", name);
        return XDocument.Load(path);
    }

    [Fact]
    public void ParseCashTransactions_RealFixture_ReturnsExpectedResult()
    {
        var doc = LoadFixture("cash-transactions.xml");

        var result = FlexResultParser.ParseCashTransactions(doc);

        result.QueryName.ShouldBe("Cash Transactions - API");
        result.CashTransactions.Count.ShouldBe(3);
        result.GeneratedAt.ShouldNotBeNull();
        result.RawXml.ShouldNotBeNull();
        result.FromDate.ShouldBe(new DateOnly(2025, 10, 13));
        result.ToDate.ShouldBe(new DateOnly(2026, 4, 8));

        var first = result.CashTransactions[0];
        first.Amount.ShouldBe(1_000_000m);
        first.Type.ShouldContain("Deposits/Withdrawals");
        first.Description.ShouldContain("ADJUSTMENT");
        first.AccountId.ShouldBe("U1234567");
    }

    [Fact]
    public void ParseTradeConfirmations_RealFixture_ReturnsExpectedResult()
    {
        var doc = LoadFixture("trade-confirmations.xml");

        var result = FlexResultParser.ParseTradeConfirmations(doc);

        result.QueryName.ShouldBe("E2E-Test");
        result.TradeConfirmations.Count.ShouldBe(39);
        result.SymbolSummaries.Count.ShouldBe(2);
        result.Orders.Count.ShouldBe(39);
        result.FromDate.ShouldBe(new DateOnly(2026, 4, 1));
        result.ToDate.ShouldBe(new DateOnly(2026, 4, 9));

        result.TradeConfirmations[0].Symbol.ShouldBe("QQQ");
        result.SymbolSummaries[0].LevelOfDetail.ShouldContain("SYMBOL_SUMMARY");
        result.Orders[0].OrderId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ParseGeneric_CashTransactionsFixture_ReturnsStatements()
    {
        var doc = LoadFixture("cash-transactions.xml");

        var result = FlexResultParser.ParseGeneric(doc);

        result.QueryType.ShouldBe("AF");
        result.QueryName.ShouldBe("Cash Transactions - API");
        result.Statements.Count.ShouldBe(128);
        result.Statements[0].AccountId.ShouldBe("U1234567");
        result.RawXml.ShouldNotBeNull();
    }

    [Fact]
    public void ParseGeneric_TradeConfirmationsFixture_ReturnsStatements()
    {
        var doc = LoadFixture("trade-confirmations.xml");

        var result = FlexResultParser.ParseGeneric(doc);

        result.QueryType.ShouldBe("TCF");
        result.Statements.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("20260201", 2026, 2, 1)]
    [InlineData("2026-02-01", 2026, 2, 1)]
    public void ParseFlexDate_ValidInput_ReturnsDate(string input, int y, int m, int d)
    {
        var result = FlexResultParser.ParseFlexDate(input);
        result.ShouldBe(new DateOnly(y, m, d));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseFlexDate_InvalidInput_ReturnsNull(string? input)
    {
        FlexResultParser.ParseFlexDate(input).ShouldBeNull();
    }

    [Fact]
    public void ParseFlexDateTime_CompactFormat_ReturnsDateTimeOffset()
    {
        var result = FlexResultParser.ParseFlexDateTime("20260409;135737");
        result.ShouldNotBeNull();
        result!.Value.Year.ShouldBe(2026);
        result.Value.Month.ShouldBe(4);
        result.Value.Day.ShouldBe(9);
        result.Value.Hour.ShouldBe(13);
        result.Value.Minute.ShouldBe(57);
        result.Value.Second.ShouldBe(37);
    }

    [Theory]
    [InlineData("2026-04-09;21:23:54 EDT")] // US abbreviations are no longer offset-guessed either (RST-3)
    [InlineData("2026-04-09;21:23:54 EST")]
    [InlineData("2026-04-09;21:23:54 CET")]
    [InlineData("2026-04-09;20:00:00 BST")]
    [InlineData("2026-04-09;08:00:00 HKT")]
    public void ParseFlexDateTime_TimezoneAbbreviation_ReturnsNullWithoutOffsetGuess(string raw)
    {
        // PVR-09 / RST-3 (§11.10 D4): the parser never guesses UTC offsets from timezone
        // abbreviations. A best-effort parse of an abbreviation-suffixed timestamp yields null
        // (rather than a fabricated, possibly-wrong offset); callers recover the raw wire string
        // from the DTO's RawElement — see ParseTradeConfirmations_TimezoneAbbreviationTimestamp_*.
        FlexResultParser.ParseFlexDateTime(raw).ShouldBeNull();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseFlexDateTime_InvalidInput_ReturnsNull(string? input)
    {
        FlexResultParser.ParseFlexDateTime(input).ShouldBeNull();
    }

    [Fact]
    public void ParseCashTransactions_EmptyElement_DoesNotThrow()
    {
        var doc = XDocument.Parse("<FlexQueryResponse queryName=\"X\" type=\"AF\"><FlexStatements><FlexStatement accountId=\"U1\"><CashTransactions><CashTransaction /></CashTransactions></FlexStatement></FlexStatements></FlexQueryResponse>");

        var result = FlexResultParser.ParseCashTransactions(doc);

        result.CashTransactions.Count.ShouldBe(1);
        var tx = result.CashTransactions[0];
        // PVR-09 (§11.10): an absent money attribute is null, never a fabricated 0.
        tx.Amount.ShouldBeNull();
        tx.AccountId.ShouldBe(string.Empty);
        tx.Conid.ShouldBeNull();
        tx.DateTime.ShouldBeNull();
    }

    [Fact]
    public void ParseCashTransactions_MissingSection_ReturnsEmptyList()
    {
        // Query configured without Cash Transactions section — no <CashTransactions> element at all
        var doc = XDocument.Parse("<FlexQueryResponse queryName=\"X\" type=\"AF\"><FlexStatements><FlexStatement accountId=\"U1\"></FlexStatement></FlexStatements></FlexQueryResponse>");

        var result = FlexResultParser.ParseCashTransactions(doc);

        result.CashTransactions.ShouldBeEmpty();
        result.QueryName.ShouldBe("X");
    }

    [Fact]
    public void ParseTradeConfirmations_MissingSections_ReturnsEmptyLists()
    {
        // Query configured with only TradeConfirms — no SymbolSummary or Order sections
        var doc = XDocument.Parse("<FlexQueryResponse queryName=\"X\" type=\"TCF\"><FlexStatements><FlexStatement accountId=\"U1\"><TradeConfirms></TradeConfirms></FlexStatement></FlexStatements></FlexQueryResponse>");

        var result = FlexResultParser.ParseTradeConfirmations(doc);

        result.TradeConfirmations.ShouldBeEmpty();
        result.SymbolSummaries.ShouldBeEmpty();
        result.Orders.ShouldBeEmpty();
        result.QueryName.ShouldBe("X");
    }

    [Fact]
    public void ParseCashTransactions_ConsolidatedFixture_ReturnsExpectedResult()
    {
        // Breakout-by-day OFF — single FlexStatement with all transactions in one block
        var doc = LoadFixture("cash-transactions-consolidated.xml");

        var result = FlexResultParser.ParseCashTransactions(doc);

        result.QueryName.ShouldBe("Cash Transactions - API");
        result.CashTransactions.Count.ShouldBe(3);
        result.CashTransactions[0].Amount.ShouldBe(1_000_000m);
        result.CashTransactions[0].Type.ShouldContain("Deposits/Withdrawals");
        result.CashTransactions[1].Amount.ShouldBe(2331.45m);
        result.CashTransactions[2].Amount.ShouldBe(2682.45m);

        // Single statement means FromDate/ToDate come directly from that one statement
        result.FromDate.ShouldBe(new DateOnly(2025, 4, 9));
        result.ToDate.ShouldBe(new DateOnly(2026, 4, 8));
        result.GeneratedAt.ShouldNotBeNull();
    }

    [Fact]
    public void ParseCashTransactions_BothFixtureShapes_ReturnSameTransactions()
    {
        // Verify that breakout-by-day ON (128 daily statements) and OFF (1 consolidated)
        // produce the same set of cash transactions — same count, same amounts, same types
        var breakoutDoc = LoadFixture("cash-transactions.xml");
        var consolidatedDoc = LoadFixture("cash-transactions-consolidated.xml");

        var breakoutResult = FlexResultParser.ParseCashTransactions(breakoutDoc);
        var consolidatedResult = FlexResultParser.ParseCashTransactions(consolidatedDoc);

        breakoutResult.CashTransactions.Count.ShouldBe(consolidatedResult.CashTransactions.Count);

        for (var i = 0; i < breakoutResult.CashTransactions.Count; i++)
        {
            breakoutResult.CashTransactions[i].Amount.ShouldBe(consolidatedResult.CashTransactions[i].Amount);
            breakoutResult.CashTransactions[i].Type.ShouldBe(consolidatedResult.CashTransactions[i].Type);
            breakoutResult.CashTransactions[i].TransactionId.ShouldBe(consolidatedResult.CashTransactions[i].TransactionId);
        }
    }

    [Fact]
    public void ParseGeneric_ConsolidatedFixture_ReturnsSingleStatement()
    {
        // Breakout-by-day OFF should produce exactly 1 FlexStatementInfo
        var doc = LoadFixture("cash-transactions-consolidated.xml");

        var result = FlexResultParser.ParseGeneric(doc);

        result.QueryType.ShouldBe("AF");
        result.Statements.Count.ShouldBe(1);
        result.Statements[0].AccountId.ShouldBe("U1234567");
        result.Statements[0].FromDate.ShouldBe(new DateOnly(2025, 4, 9));
        result.Statements[0].ToDate.ShouldBe(new DateOnly(2026, 4, 8));
    }

    [Fact]
    public void ParseTradeConfirmations_OnlyTradeConfirmsPresent_OtherListsEmpty()
    {
        // Query has trades but no summaries or orders
        var doc = XDocument.Parse("""
            <FlexQueryResponse queryName="X" type="TCF">
              <FlexStatements>
                <FlexStatement accountId="U1">
                  <TradeConfirms>
                    <TradeConfirm accountId="U1" symbol="SPY" buySell="BUY" quantity="1" price="650" />
                  </TradeConfirms>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = FlexResultParser.ParseTradeConfirmations(doc);

        result.TradeConfirmations.Count.ShouldBe(1);
        result.TradeConfirmations[0].Symbol.ShouldBe("SPY");
        result.SymbolSummaries.ShouldBeEmpty();
        result.Orders.ShouldBeEmpty();
    }

    // ---- PVR-09 / RST-1: nullable money + observable parse-failure signal + raw text ----

    [Fact]
    public void ParseTradeConfirmations_UnparseableMoneyAttributes_YieldNullAndRaiseSignalWithRawText()
    {
        var failures = new List<(string Field, string Raw)>();
        var doc = XDocument.Parse("""
            <FlexQueryResponse queryName="X" type="TCF">
              <FlexStatements>
                <FlexStatement accountId="U1">
                  <TradeConfirms>
                    <TradeConfirm accountId="U1" symbol="SPY" buySell="BUY"
                      quantity="abc" price="not-a-number" amount="N/A"
                      proceeds="--" netCash="oops" commission="?" />
                  </TradeConfirms>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = FlexResultParser.ParseTradeConfirmations(
            doc, (field, raw) => failures.Add((field, raw)));

        var tc = result.TradeConfirmations.ShouldHaveSingleItem();

        // Unparseable-but-present money → null, never a fabricated 0.
        tc.Quantity.ShouldBeNull();
        tc.Price.ShouldBeNull();
        tc.Amount.ShouldBeNull();
        tc.Proceeds.ShouldBeNull();
        tc.NetCash.ShouldBeNull();
        tc.Commission.ShouldBeNull();

        // Observable parse-failure signal fires once per unparseable money field.
        failures.Select(f => f.Field).ShouldBe(
            new[] { "quantity", "price", "amount", "proceeds", "netCash", "commission" },
            ignoreOrder: true);

        // Raw wire text preserved — both on the signal and recoverable from RawElement,
        // so a bad value is distinguishable from a genuine 0 (0m) or an absent value (null).
        failures.Single(f => f.Field == "amount").Raw.ShouldBe("N/A");
        tc.RawElement!.Attribute("price")!.Value.ShouldBe("not-a-number");
    }

    [Fact]
    public void ParseTradeConfirmations_GenuineZeroAndAbsentMoney_AreDistinctFromUnparseable()
    {
        var failures = new List<(string Field, string Raw)>();
        var doc = XDocument.Parse("""
            <FlexQueryResponse queryName="X" type="TCF">
              <FlexStatements>
                <FlexStatement accountId="U1">
                  <TradeConfirms>
                    <TradeConfirm accountId="U1" symbol="SPY" commission="0" />
                  </TradeConfirms>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = FlexResultParser.ParseTradeConfirmations(
            doc, (field, raw) => failures.Add((field, raw)));

        var tc = result.TradeConfirmations.ShouldHaveSingleItem();

        // Genuine zero → parsed value 0m (present, parseable), no signal.
        tc.Commission.ShouldBe(0m);
        // Absent → null, no signal, and no raw attribute to recover.
        tc.Amount.ShouldBeNull();
        tc.RawElement!.Attribute("amount").ShouldBeNull();
        // Neither a genuine 0 nor an absent value raises a parse-failure signal.
        failures.ShouldBeEmpty();
    }

    [Fact]
    public void ParseCashTransactions_UnparseableAmountAndFxRate_YieldNullAndRaiseSignal()
    {
        var failures = new List<(string Field, string Raw)>();
        var doc = XDocument.Parse("""
            <FlexQueryResponse queryName="X" type="AF">
              <FlexStatements>
                <FlexStatement accountId="U1">
                  <CashTransactions>
                    <CashTransaction accountId="U1" amount="abc" fxRateToBase="xyz" />
                  </CashTransactions>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = FlexResultParser.ParseCashTransactions(
            doc, (field, raw) => failures.Add((field, raw)));

        var ct = result.CashTransactions.ShouldHaveSingleItem();

        ct.Amount.ShouldBeNull();
        ct.FxRateToBase.ShouldBeNull();
        failures.ShouldContain(("amount", "abc"));
        failures.ShouldContain(("fxRateToBase", "xyz"));
        ct.RawElement!.Attribute("amount")!.Value.ShouldBe("abc");
    }

    [Fact]
    public void ParseTradeConfirmations_EmptyMoneyAttribute_YieldNullWithoutSignal()
    {
        // A present-but-empty attribute (the empty-money wire convention) is null, not a
        // parse failure — it must not raise a false parse-failure signal (ADR-0001 / §11.10).
        var failures = new List<(string Field, string Raw)>();
        var doc = XDocument.Parse("""
            <FlexQueryResponse queryName="X" type="TCF">
              <FlexStatements>
                <FlexStatement accountId="U1">
                  <TradeConfirms>
                    <TradeConfirm accountId="U1" symbol="SPY" price="" amount="" />
                  </TradeConfirms>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = FlexResultParser.ParseTradeConfirmations(
            doc, (field, raw) => failures.Add((field, raw)));

        var tc = result.TradeConfirmations.ShouldHaveSingleItem();
        tc.Price.ShouldBeNull();
        tc.Amount.ShouldBeNull();
        failures.ShouldBeEmpty();
    }

    // ---- PVR-09 / RST-3: raw timestamp preservation, no offset guessing ----

    [Fact]
    public void ParseTradeConfirmations_TimezoneAbbreviationTimestamp_PreservesRawStringNoOffsetGuess()
    {
        var doc = XDocument.Parse("""
            <FlexQueryResponse queryName="X" type="TCF">
              <FlexStatements>
                <FlexStatement accountId="U1">
                  <TradeConfirms>
                    <TradeConfirm accountId="U1" symbol="VOD"
                      dateTime="2026-04-09;21:23:54 CET" orderTime="2026-04-09;20:00:00 BST" />
                  </TradeConfirms>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = FlexResultParser.ParseTradeConfirmations(doc);
        var tc = result.TradeConfirmations.ShouldHaveSingleItem();

        // Best-effort parse yields null rather than a fabricated (wrong) offset for a
        // non-US abbreviation the parser cannot resolve...
        tc.DateTime.ShouldBeNull();
        tc.OrderTime.ShouldBeNull();

        // ...but the raw wire string is preserved and recoverable, so the timestamp is not lost.
        tc.RawElement!.Attribute("dateTime")!.Value.ShouldBe("2026-04-09;21:23:54 CET");
        tc.RawElement!.Attribute("orderTime")!.Value.ShouldBe("2026-04-09;20:00:00 BST");
    }
}
