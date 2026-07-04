using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using IbkrConduit.Orders;
using IbkrConduit.Serialization;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Orders;

/// <summary>
/// Guards that the order-response DTOs map the fields IBKR actually returns, so
/// <c>ResponseSchemaValidationHandler</c> no longer logs an "Extra fields" warning for them.
/// Deserializes through the exact serializer every Refit client in the library uses.
/// </summary>
public class OrderResponseSchemaFieldsTests
{
    private static Task<T?> DeserializeAsync<T>(string json)
    {
        var serializer = IbkrRefitSettings.Create().ContentSerializer;
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return serializer.FromHttpContentAsync<T>(content, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OrdersResponse_DeserializesSnapshotFlag_False()
    {
        var result = await DeserializeAsync<OrdersResponse>("""{"orders":[],"snapshot":false}""");

        result.ShouldNotBeNull();
        result!.Snapshot.ShouldBe(false);
    }

    [Fact]
    public async Task OrdersResponse_DeserializesSnapshotFlag_True()
    {
        var result = await DeserializeAsync<OrdersResponse>("""{"orders":[],"snapshot":true}""");

        result.ShouldNotBeNull();
        result!.Snapshot.ShouldBe(true);
    }

    [Fact]
    public async Task OrdersResponse_SnapshotAbsent_IsNull()
    {
        var result = await DeserializeAsync<OrdersResponse>("""{"orders":[]}""");

        result.ShouldNotBeNull();
        result!.Snapshot.ShouldBeNull();
    }

    [Fact]
    public async Task OrderSubmissionResponse_DeserializesEncryptMessage()
    {
        var result = await DeserializeAsync<OrderSubmissionResponse>(
            """{"order_id":"1234567890","order_status":"Submitted","encrypt_message":"1"}""");

        result.ShouldNotBeNull();
        result!.EncryptMessage.ShouldBe("1");
        result.OrderId.ShouldBe("1234567890");
    }

    [Fact]
    public async Task OrderSubmissionResponse_EncryptMessageAbsent_IsNull()
    {
        var result = await DeserializeAsync<OrderSubmissionResponse>(
            """{"order_id":"1234567890","order_status":"Submitted"}""");

        result.ShouldNotBeNull();
        result!.EncryptMessage.ShouldBeNull();
    }

    [Fact]
    public async Task LiveOrder_DeserializesLimitPrice_FromString()
    {
        // IBKR returns the working limit price as a quoted string.
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":888626139,"ticker":"QQQ","side":"BUY","status":"PreSubmitted","orderType":"Limit","price":"400.00"}""");

        result.ShouldNotBeNull();
        result!.Price.ShouldBe(400.00m);
    }

    [Fact]
    public async Task LiveOrder_MarketOrderEmptyPrice_MapsToNull()
    {
        // Market orders carry price="" — must map to null, not throw.
        var result = await DeserializeAsync<LiveOrder>(
            """{"orderId":201804948,"ticker":"QQQ","side":"BUY","status":"PreSubmitted","orderType":"Market","price":""}""");

        result.ShouldNotBeNull();
        result!.Price.ShouldBeNull();
    }

    [Fact]
    public async Task Trade_DeserializesAllDocumentedFields()
    {
        // The full /iserver/account/trades object shape (IBKR spec example).
        var result = await DeserializeAsync<Trade>(
            """
            {
              "execution_id":"0000e0d5.6576fd38.01.01","symbol":"AAPL","supports_tax_opt":"1",
              "side":"S","order_description":"Sold 5 @ 192.26 on ISLAND",
              "trade_time":"20231211-18:00:49","trade_time_r":1702317649000,"size":5.0,
              "price":"192.26","order_ref":"Order123","submitter":"user1234","exchange":"ISLAND",
              "commission":"1.01","net_amount":961.3,"account":"U1234567","accountCode":"U1234567",
              "company_name":"APPLE INC","contract_description_1":"AAPL","sec_type":"STK",
              "listing_exchange":"NASDAQ.NMS","conid":265598,"conidEx":"265598","clearing_id":"IB",
              "clearing_name":"IB","liquidation_trade":"0","is_event_trading":"0",
              "order_id":656804954,"position":"5","account_allocation_name":"U1234567"
            }
            """);

        result.ShouldNotBeNull();
        result!.Commission.ShouldBe(1.01m);   // quoted string -> decimal
        result.NetAmount.ShouldBe(961.3m);
        result.TradeTime.ShouldBe("20231211-18:00:49");
        result.TradeTimeR.ShouldBe(1702317649000);
        result.Exchange.ShouldBe("ISLAND");
        result.Account.ShouldBe("U1234567");
        result.AccountCode.ShouldBe("U1234567");
        result.CompanyName.ShouldBe("APPLE INC");
        result.ContractDescription1.ShouldBe("AAPL");
        result.SecType.ShouldBe("STK");
        result.ListingExchange.ShouldBe("NASDAQ.NMS");
        result.ConidEx.ShouldBe("265598");
        result.ClearingId.ShouldBe("IB");
        result.ClearingName.ShouldBe("IB");
        result.SupportsTaxOpt.ShouldBe(true);   // "1" -> true
        result.OrderDescription.ShouldBe("Sold 5 @ 192.26 on ISLAND");
        result.LiquidationTrade.ShouldBe(false); // "0" -> false
        result.IsEventTrading.ShouldBe(false);   // "0" -> false
        result.OrderId.ShouldBe("656804954");    // JSON number -> string
        result.Position.ShouldBe(5m);            // "5" -> decimal
        result.AccountAllocationName.ShouldBe("U1234567");
        // Fully covered — nothing spills into AdditionalData.
        result.AdditionalData.ShouldBeNull();
    }

    [Fact]
    public async Task Trade_EmptyCommission_MapsToNull()
    {
        var result = await DeserializeAsync<Trade>(
            """{"execution_id":"x","conid":1,"symbol":"A","side":"B","size":1,"price":1,"order_ref":"r","submitter":"u","commission":""}""");

        result.ShouldNotBeNull();
        result!.Commission.ShouldBeNull();
    }
}
