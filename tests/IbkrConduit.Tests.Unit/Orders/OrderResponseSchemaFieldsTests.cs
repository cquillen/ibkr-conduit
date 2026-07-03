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
}
