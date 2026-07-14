using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IbkrConduit.Accounts;
using IbkrConduit.EventContracts;
using IbkrConduit.Portfolio;
using IbkrConduit.Serialization;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Serialization;

/// <summary>
/// Presence + precision pins for the PVR-02 REST money DTOs (Position, LedgerEntry, the account-summary
/// overview/cash-balance families, and the event-contract strike/payout fields). Every money field is
/// <c>decimal?</c> and nullable-as-presence (§6.5, ADR-0001): an absent or empty-string wire value
/// deserializes to <see langword="null"/> — never a fabricated <c>0</c> — and a populated value
/// round-trips with full decimal precision that a <c>double</c> mapping would corrupt. These deserialize
/// through the library's actual Refit content serializer so they exercise the same empty-tolerant
/// converters every registered client uses.
/// </summary>
public class RestMoneyDtoPresenceTests
{
    // A 19-significant-digit monetary value that binary double cannot represent exactly; decimal holds
    // it verbatim, so an exact-equality assertion fails loudly if a field is ever mapped as double again.
    private const decimal _highPrecision = 1234567890.123456789m;

    private static Task<T?> DeserializeAsync<T>(string json)
    {
        var serializer = IbkrRefitSettings.Create().ContentSerializer;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return serializer.FromHttpContentAsync<T>(content, TestContext.Current.CancellationToken);
    }

    // ---- Position ------------------------------------------------------------------------------

    [Fact]
    public async Task Position_OmittedMoneyField_DeserializesToNull()
    {
        // mktValue and unrealizedPnl omitted entirely from the row.
        var json = """
            {"acctId":"U1","conid":1,"contractDesc":"QQQ","position":3.0,"mktPrice":10.0,
             "avgCost":10.0,"avgPrice":10.0,"realizedPnl":0.0,"currency":"USD"}
            """;

        var pos = await DeserializeAsync<Position>(json);

        pos.ShouldNotBeNull();
        pos!.MarketValue.ShouldBeNull();
        pos.UnrealizedPnl.ShouldBeNull();
    }

    [Fact]
    public async Task Position_EmptyStringMoneyField_DeserializesToNull()
    {
        var json = """
            {"acctId":"U1","conid":1,"contractDesc":"QQQ","position":"","mktPrice":"",
             "mktValue":"","avgCost":"","avgPrice":"","realizedPnl":"","unrealizedPnl":"","currency":"USD"}
            """;

        var pos = await DeserializeAsync<Position>(json);

        pos.ShouldNotBeNull();
        pos!.Quantity.ShouldBeNull();
        pos.MarketPrice.ShouldBeNull();
        pos.MarketValue.ShouldBeNull();
        pos.AverageCost.ShouldBeNull();
        pos.AveragePrice.ShouldBeNull();
        pos.RealizedPnl.ShouldBeNull();
        pos.UnrealizedPnl.ShouldBeNull();
    }

    [Fact]
    public async Task Position_PopulatedMoneyField_PreservesDecimalPrecision()
    {
        var json = $$"""
            {"acctId":"U1","conid":1,"contractDesc":"QQQ","position":3.0,"mktPrice":10.0,
             "mktValue":{{_highPrecision}},"avgCost":{{_highPrecision}},"avgPrice":10.0,
             "realizedPnl":0.0,"unrealizedPnl":1.1,"currency":"USD"}
            """;

        var pos = await DeserializeAsync<Position>(json);

        pos.ShouldNotBeNull();
        pos!.MarketValue.ShouldBe(_highPrecision);
        pos.AverageCost.ShouldBe(_highPrecision);
    }

    // ---- LedgerEntry ---------------------------------------------------------------------------

    [Fact]
    public async Task LedgerEntry_OmittedAndEmptyMoneyFields_DeserializeToNull()
    {
        // dividends omitted; cashbalance empty string.
        var json = """
            {"currency":"USD","cashbalance":"","netliquidationvalue":100.0,"settledcash":50.0,
             "acctcode":"U1","key":"LedgerList","timestamp":1,"severity":0,"secondkey":"USD","endofbundle":1}
            """;

        var entry = await DeserializeAsync<LedgerEntry>(json);

        entry.ShouldNotBeNull();
        entry!.CashBalance.ShouldBeNull();
        entry.Dividends.ShouldBeNull();
        entry.CryptocurrencyValue.ShouldBeNull();
    }

    [Fact]
    public async Task LedgerEntry_PopulatedMoneyField_PreservesDecimalPrecision()
    {
        var json = $$"""
            {"currency":"USD","cashbalance":{{_highPrecision}},"netliquidationvalue":{{_highPrecision}},
             "settledcash":50.0,"timestamp":1,"severity":0,"endofbundle":1}
            """;

        var entry = await DeserializeAsync<LedgerEntry>(json);

        entry.ShouldNotBeNull();
        entry!.CashBalance.ShouldBe(_highPrecision);
        entry.NetLiquidationValue.ShouldBe(_highPrecision);
    }

    // ---- LedgerEntry.EndOfBundle (RPD-07) — per-response marker, per-entry nullable-as-presence -

    [Fact]
    public async Task LedgerEntry_EndOfBundlePresent_DeserializesToValue()
    {
        // Per the live probe (recordings/ledger-endofbundle-probe/): endofbundle is present (value 1)
        // on the real-currency (USD) entry.
        var json = """
            {"currency":"USD","secondkey":"USD","timestamp":1,"severity":0,"endofbundle":1}
            """;

        var entry = await DeserializeAsync<LedgerEntry>(json);

        entry.ShouldNotBeNull();
        entry!.EndOfBundle.ShouldBe(1);
    }

    [Fact]
    public async Task LedgerEntry_EndOfBundleOmitted_DeserializesToNull()
    {
        // Per the live probe: endofbundle is absent (not a fabricated 0) on the BASE entry.
        // Guards the ADR-0001 violation the probe found: modeling this as non-nullable int silently
        // defaults an absent field to 0, indistinguishable from a real 0.
        var json = """
            {"currency":"BASE","secondkey":"BASE","timestamp":1,"severity":0}
            """;

        var entry = await DeserializeAsync<LedgerEntry>(json);

        entry.ShouldNotBeNull();
        entry!.EndOfBundle.ShouldBeNull();
    }

    // ---- AccountSummaryOverview / AccountSummaryCashBalance -------------------------------------

    [Fact]
    public async Task AccountSummaryOverview_OmittedAndEmptyMoneyFields_DeserializeToNull()
    {
        // balance omitted; buyingPower empty string.
        var json = """
            {"accountType":"","status":"","buyingPower":"","netLiquidationValue":100.0}
            """;

        var overview = await DeserializeAsync<AccountSummaryOverview>(json);

        overview.ShouldNotBeNull();
        overview!.Balance.ShouldBeNull();
        overview.BuyingPower.ShouldBeNull();
        overview.InitialMargin.ShouldBeNull();
    }

    [Fact]
    public async Task AccountSummaryOverview_PopulatedMoneyField_PreservesDecimalPrecision()
    {
        var json = $$"""
            {"balance":{{_highPrecision}},"netLiquidationValue":{{_highPrecision}},"buyingPower":10.0}
            """;

        var overview = await DeserializeAsync<AccountSummaryOverview>(json);

        overview.ShouldNotBeNull();
        overview!.Balance.ShouldBe(_highPrecision);
        overview.NetLiquidationValue.ShouldBe(_highPrecision);
    }

    [Fact]
    public async Task AccountSummaryCashBalance_PresenceAndPrecision()
    {
        // balance high-precision present; settledCash empty string → null.
        var json = $$"""
            {"currency":"USD","balance":{{_highPrecision}},"settledCash":""}
            """;

        var cash = await DeserializeAsync<AccountSummaryCashBalance>(json);

        cash.ShouldNotBeNull();
        cash!.Balance.ShouldBe(_highPrecision);
        cash.SettledCash.ShouldBeNull();
    }

    // ---- Event contracts -----------------------------------------------------------------------

    [Fact]
    public async Task EventContract_StrikePresenceAndPrecision()
    {
        var omittedJson = """{"conid":1,"side":"Y","expiration":"20270127","strike_label":"x"}""";
        var omitted = await DeserializeAsync<EventContract>(omittedJson);
        omitted.ShouldNotBeNull();
        omitted!.Strike.ShouldBeNull();

        var preciseJson = $$"""{"conid":1,"side":"Y","strike":{{_highPrecision}}}""";
        var precise = await DeserializeAsync<EventContract>(preciseJson);
        precise.ShouldNotBeNull();
        precise!.Strike.ShouldBe(_highPrecision);
    }

    [Fact]
    public async Task EventContractMarketResponse_PayoutPresenceAndPrecision()
    {
        var emptyJson = """
            {"market_name":"m","exchange":"e","symbol":"s","logo_category":"g",
             "exclude_historical_data":false,"payout":"","contracts":[]}
            """;
        var empty = await DeserializeAsync<EventContractMarketResponse>(emptyJson);
        empty.ShouldNotBeNull();
        empty!.Payout.ShouldBeNull();

        var preciseJson = $$"""
            {"market_name":"m","exchange":"e","symbol":"s","logo_category":"g",
             "exclude_historical_data":false,"payout":{{_highPrecision}},"contracts":[]}
            """;
        var precise = await DeserializeAsync<EventContractMarketResponse>(preciseJson);
        precise.ShouldNotBeNull();
        precise!.Payout.ShouldBe(_highPrecision);
    }

    [Fact]
    public async Task EventContractDetailsResponse_StrikeAndPayoutPresenceAndPrecision()
    {
        var omittedJson = """{"conid_yes":1,"conid_no":2,"question":"q","side":"Y"}""";
        var omitted = await DeserializeAsync<EventContractDetailsResponse>(omittedJson);
        omitted.ShouldNotBeNull();
        omitted!.Strike.ShouldBeNull();
        omitted.PayoutAmount.ShouldBeNull();

        var preciseJson = $$"""
            {"conid_yes":1,"conid_no":2,"strike":{{_highPrecision}},"payout":{{_highPrecision}}}
            """;
        var precise = await DeserializeAsync<EventContractDetailsResponse>(preciseJson);
        precise.ShouldNotBeNull();
        precise!.Strike.ShouldBe(_highPrecision);
        precise.PayoutAmount.ShouldBe(_highPrecision);
    }
}
