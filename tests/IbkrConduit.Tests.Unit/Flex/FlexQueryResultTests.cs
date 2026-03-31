using System.Xml.Linq;
using IbkrConduit.Flex;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Flex;

public class FlexQueryResultTests
{
    [Fact]
    public void Trades_ParsesTradeElements()
    {
        var xml = XDocument.Parse("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1234567">
                  <Trades>
                    <Trade accountId="U1234567" symbol="AAPL" conid="265598"
                           description="APPLE INC" buySell="BUY" quantity="100"
                           price="150.50" proceeds="-15050" commission="-1.00"
                           currency="USD" tradeDate="20260301" tradeTime="093000"
                           orderType="MKT" exchange="SMART" orderId="12345" execId="E001" />
                  </Trades>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = new FlexQueryResult(xml);

        result.Trades.Count.ShouldBe(1);
        var trade = result.Trades[0];
        trade.AccountId.ShouldBe("U1234567");
        trade.Symbol.ShouldBe("AAPL");
        trade.Conid.ShouldBe(265598);
        trade.Description.ShouldBe("APPLE INC");
        trade.Side.ShouldBe("BUY");
        trade.Quantity.ShouldBe(100m);
        trade.Price.ShouldBe(150.50m);
        trade.Proceeds.ShouldBe(-15050m);
        trade.Commission.ShouldBe(-1.00m);
        trade.Currency.ShouldBe("USD");
        trade.TradeDate.ShouldBe("20260301");
        trade.TradeTime.ShouldBe("093000");
        trade.OrderType.ShouldBe("MKT");
        trade.Exchange.ShouldBe("SMART");
        trade.OrderId.ShouldBe("12345");
        trade.ExecId.ShouldBe("E001");
        trade.RawElement.ShouldNotBeNull();
    }

    [Fact]
    public void Trades_ParsesTradeConfirmationElements()
    {
        var xml = XDocument.Parse("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1234567">
                  <TradeConfirmations>
                    <TradeConfirmation accountId="U1234567" symbol="MSFT" conid="272093"
                           description="MICROSOFT CORP" buySell="SELL" quantity="50"
                           price="400.00" proceeds="20000" commission="-0.65"
                           currency="USD" tradeDate="20260315" tradeTime="100000"
                           orderType="LMT" exchange="NASDAQ" orderId="67890" execId="E002" />
                  </TradeConfirmations>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = new FlexQueryResult(xml);

        result.Trades.Count.ShouldBe(1);
        var trade = result.Trades[0];
        trade.Symbol.ShouldBe("MSFT");
        trade.Side.ShouldBe("SELL");
        trade.Quantity.ShouldBe(50m);
    }

    [Fact]
    public void OpenPositions_ParsesPositionElements()
    {
        var xml = XDocument.Parse("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1234567">
                  <OpenPositions>
                    <OpenPosition accountId="U1234567" symbol="SPY" conid="756733"
                           description="SPDR S&amp;P 500 ETF" position="200"
                           markPrice="550.00" positionValue="110000" costBasisMoney="100000"
                           fifoPnlUnrealized="10000" currency="USD" assetCategory="STK" />
                  </OpenPositions>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = new FlexQueryResult(xml);

        result.OpenPositions.Count.ShouldBe(1);
        var pos = result.OpenPositions[0];
        pos.AccountId.ShouldBe("U1234567");
        pos.Symbol.ShouldBe("SPY");
        pos.Conid.ShouldBe(756733);
        pos.Description.ShouldBe("SPDR S&P 500 ETF");
        pos.Position.ShouldBe(200m);
        pos.MarkPrice.ShouldBe(550.00m);
        pos.PositionValue.ShouldBe(110000m);
        pos.CostBasis.ShouldBe(100000m);
        pos.UnrealizedPnl.ShouldBe(10000m);
        pos.Currency.ShouldBe("USD");
        pos.AssetClass.ShouldBe("STK");
        pos.RawElement.ShouldNotBeNull();
    }

    [Fact]
    public void Trades_MissingSection_ReturnsEmptyList()
    {
        var xml = XDocument.Parse("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1234567">
                  <OpenPositions>
                    <OpenPosition accountId="U1234567" symbol="SPY" conid="756733" />
                  </OpenPositions>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = new FlexQueryResult(xml);

        result.Trades.ShouldBeEmpty();
    }

    [Fact]
    public void OpenPositions_MissingSection_ReturnsEmptyList()
    {
        var xml = XDocument.Parse("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1234567">
                  <Trades>
                    <Trade accountId="U1234567" symbol="AAPL" conid="265598" />
                  </Trades>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = new FlexQueryResult(xml);

        result.OpenPositions.ShouldBeEmpty();
    }

    [Fact]
    public void Trades_InvalidConid_ReturnsNull()
    {
        var xml = XDocument.Parse("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1234567">
                  <Trades>
                    <Trade accountId="U1234567" symbol="AAPL" conid="notanumber" />
                  </Trades>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = new FlexQueryResult(xml);

        result.Trades[0].Conid.ShouldBeNull();
    }

    [Fact]
    public void Trades_MissingAttributes_DefaultToEmpty()
    {
        var xml = XDocument.Parse("""
            <FlexQueryResponse>
              <FlexStatements count="1">
                <FlexStatement accountId="U1234567">
                  <Trades>
                    <Trade />
                  </Trades>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = new FlexQueryResult(xml);

        var trade = result.Trades[0];
        trade.AccountId.ShouldBe(string.Empty);
        trade.Symbol.ShouldBe(string.Empty);
        trade.Conid.ShouldBeNull();
        trade.Quantity.ShouldBe(0m);
        trade.Price.ShouldBe(0m);
    }

    [Fact]
    public void RawXml_PreservesOriginalDocument()
    {
        var xml = XDocument.Parse("<FlexQueryResponse><FlexStatements count=\"0\" /></FlexQueryResponse>");

        var result = new FlexQueryResult(xml);

        result.RawXml.ShouldBeSameAs(xml);
    }

    [Fact]
    public void MultipleStatements_AggregatesTradesAcrossStatements()
    {
        var xml = XDocument.Parse("""
            <FlexQueryResponse>
              <FlexStatements count="2">
                <FlexStatement accountId="U1111111">
                  <Trades>
                    <Trade accountId="U1111111" symbol="AAPL" conid="265598" />
                  </Trades>
                </FlexStatement>
                <FlexStatement accountId="U2222222">
                  <Trades>
                    <Trade accountId="U2222222" symbol="MSFT" conid="272093" />
                  </Trades>
                </FlexStatement>
              </FlexStatements>
            </FlexQueryResponse>
            """);

        var result = new FlexQueryResult(xml);

        result.Trades.Count.ShouldBe(2);
        result.Trades[0].AccountId.ShouldBe("U1111111");
        result.Trades[1].AccountId.ShouldBe("U2222222");
    }
}
