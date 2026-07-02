using IbkrConduit.Contracts;
using IbkrConduit.Examples.OrderSubmit;
using Shouldly;

namespace IbkrConduit.Examples.Tests.OrderSubmit;

public class ContractSelectionTests
{
    // The exchange for a symbol-search result is carried in Description (e.g. "ARCA", "MEXI").
    private static ContractSearchResult Stock(int conid, string symbol, string exchange) =>
        new(
            conid,                     // Conid
            null,                      // BondId
            $"{symbol} - {exchange}",  // CompanyHeader
            $"{symbol} INC",           // CompanyName
            symbol,                    // Symbol
            exchange,                  // Description (carries the exchange)
            null,                      // Restricted
            null,                      // Fop
            null,                      // Opt
            null,                      // War
            null,                      // Sections
            null);                     // Issuers

    [Fact]
    public void SelectStockContract_PrefersUsListing_EvenWhenNotFirst()
    {
        var matches = new List<ContractSearchResult>
        {
            Stock(1, "SPY", "MEXI"),
            Stock(2, "SPY", "ARCA"),
        };

        var chosen = Program.SelectStockContract(matches, "SPY");

        chosen.ShouldNotBeNull();
        chosen!.Conid.ShouldBe(2);
        chosen.Description.ShouldBe("ARCA"); // Description carries the exchange
    }

    [Fact]
    public void SelectStockContract_NoUsListing_FallsBackToFirstExactMatch()
    {
        var matches = new List<ContractSearchResult>
        {
            Stock(10, "SPY", "MEXI"),
            Stock(11, "SPY", "LSE"),
        };

        var chosen = Program.SelectStockContract(matches, "SPY");

        chosen.ShouldNotBeNull();
        chosen!.Conid.ShouldBe(10);
    }

    [Fact]
    public void SelectStockContract_NoSymbolMatch_ReturnsNull()
    {
        var matches = new List<ContractSearchResult>
        {
            Stock(1, "QQQ", "NASDAQ"),
        };

        Program.SelectStockContract(matches, "SPY").ShouldBeNull();
    }

    [Fact]
    public void SelectStockContract_IgnoresNonMatchingSymbols()
    {
        var matches = new List<ContractSearchResult>
        {
            Stock(1, "SPYG", "ARCA"),  // different symbol, US-listed — must be ignored
            Stock(2, "SPY", "MEXI"),   // exact symbol, foreign — the only real match
        };

        var chosen = Program.SelectStockContract(matches, "SPY");

        chosen.ShouldNotBeNull();
        chosen!.Conid.ShouldBe(2);
    }

    [Fact]
    public void SelectStockContract_MatchesSymbolCaseInsensitively()
    {
        var matches = new List<ContractSearchResult>
        {
            Stock(5, "spy", "NYSE"),
        };

        var chosen = Program.SelectStockContract(matches, "SPY");

        chosen.ShouldNotBeNull();
        chosen!.Conid.ShouldBe(5);
    }

    [Theory]
    [InlineData("NASDAQ")]
    [InlineData("NYSE")]
    [InlineData("ARCA")]
    [InlineData("AMEX")]
    [InlineData("BATS")]
    [InlineData("nasdaq")]  // case-insensitive
    public void IsUsPrimaryExchange_UsVenues_ReturnTrue(string exchange)
    {
        Program.IsUsPrimaryExchange(exchange).ShouldBeTrue();
    }

    [Theory]
    [InlineData("MEXI")]
    [InlineData("LSE")]
    [InlineData("")]
    [InlineData(null)]
    public void IsUsPrimaryExchange_NonUsOrEmpty_ReturnFalse(string? exchange)
    {
        Program.IsUsPrimaryExchange(exchange).ShouldBeFalse();
    }
}
