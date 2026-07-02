using IbkrConduit.Examples.OrderSubmit;
using Shouldly;

namespace IbkrConduit.Examples.Tests.OrderSubmit;

public class ArgParsingTests
{
    [Fact]
    public void TryParseArgs_MarketBuy_ParsesPositionalsAndDefaults()
    {
        Program.TryParseArgs(new[] { "BUY", "100", "AAPL" }, out var o, out _).ShouldBeTrue();
        o.ShouldNotBeNull();
        o!.Side.ShouldBe("BUY");
        o.Quantity.ShouldBe(100);
        o.Symbol.ShouldBe("AAPL");
        o.OrderType.ShouldBe("MKT");
        o.Price.ShouldBeNull();
        o.Tif.ShouldBe("DAY");
        o.AutoConfirm.ShouldBeFalse();
        o.WhatIf.ShouldBeFalse();
    }

    [Fact]
    public void TryParseArgs_LowercaseSideAndSymbol_AreNormalizedToUpper()
    {
        Program.TryParseArgs(new[] { "buy", "5", "aapl" }, out var o, out _).ShouldBeTrue();
        o!.Side.ShouldBe("BUY");
        o.Symbol.ShouldBe("AAPL");
    }

    [Fact]
    public void TryParseArgs_Limit_SetsTypeAndPrice()
    {
        Program.TryParseArgs(new[] { "BUY", "1", "QQQ", "--limit", "500" }, out var o, out _).ShouldBeTrue();
        o!.OrderType.ShouldBe("LMT");
        o.Price.ShouldBe(500m);
    }

    [Fact]
    public void TryParseArgs_MarketAndLimitTogether_ReturnsError()
    {
        Program.TryParseArgs(new[] { "BUY", "1", "QQQ", "--market", "--limit", "5" }, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TryParseArgs_TifAndFlags_AreParsed()
    {
        Program.TryParseArgs(new[] { "SELL", "2", "SPY", "--tif", "GTC", "--yes", "--what-if" }, out var o, out _).ShouldBeTrue();
        o!.Side.ShouldBe("SELL");
        o.Tif.ShouldBe("GTC");
        o.AutoConfirm.ShouldBeTrue();
        o.WhatIf.ShouldBeTrue();
    }

    [Fact]
    public void TryParseArgs_OrderRefAndAccount_AreParsed()
    {
        Program.TryParseArgs(new[] { "BUY", "1", "AAPL", "--order-ref", "r1", "--account", "DU123" }, out var o, out _).ShouldBeTrue();
        o!.OrderRef.ShouldBe("r1");
        o.Account.ShouldBe("DU123");
    }

    [Theory]
    [InlineData("HOLD", "1", "AAPL")]   // bad side
    [InlineData("BUY", "0", "AAPL")]    // qty not > 0
    [InlineData("BUY", "abc", "AAPL")]  // qty non-numeric
    public void TryParseArgs_InvalidPositionals_ReturnError(string side, string qty, string symbol)
    {
        Program.TryParseArgs(new[] { side, qty, symbol }, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TryParseArgs_LimitWithoutPrice_ReturnsError()
    {
        Program.TryParseArgs(new[] { "BUY", "1", "AAPL", "--limit" }, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TryParseArgs_BadTif_ReturnsError()
    {
        Program.TryParseArgs(new[] { "BUY", "1", "AAPL", "--tif", "FOO" }, out _, out var error).ShouldBeFalse();
        error.ShouldContain("--tif");
    }

    [Fact]
    public void TryParseArgs_UnknownFlag_ReturnsError()
    {
        Program.TryParseArgs(new[] { "BUY", "1", "AAPL", "--bogus" }, out _, out var error).ShouldBeFalse();
        error.ShouldContain("--bogus");
    }

    [Theory]
    [InlineData("BUY", "100")]                  // too few positionals
    public void TryParseArgs_TooFewPositionals_ReturnsError(params string[] args)
    {
        Program.TryParseArgs(args, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void TryParseArgs_TooManyPositionals_ReturnsError()
    {
        Program.TryParseArgs(new[] { "BUY", "100", "AAPL", "EXTRA" }, out _, out var error).ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
    }
}
