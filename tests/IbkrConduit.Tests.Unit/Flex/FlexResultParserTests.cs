using System;
using System.IO;
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

    [Fact]
    public void ParseFlexDateTime_EdtFormat_ReturnsDateTimeOffset()
    {
        var result = FlexResultParser.ParseFlexDateTime("2026-04-09;21:23:54 EDT");
        result.ShouldNotBeNull();
        result!.Value.Year.ShouldBe(2026);
        result.Value.Month.ShouldBe(4);
        result.Value.Day.ShouldBe(9);
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
        tx.Amount.ShouldBe(0m);
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
}
