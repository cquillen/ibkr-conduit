using IbkrConduit.Contracts;
using IbkrConduit.Http;
using IbkrConduit.Orders;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

/// <summary>
/// RPD-01: proves the newly-promoted <see cref="LiveOrder"/>, <see cref="ContractSearchResult"/>,
/// and <see cref="CancelOrderResponse"/> fields are recognized by <see cref="DtoFieldMap"/> — the
/// mechanism <c>ResponseSchemaValidationHandler</c> uses to flag "Extra fields" warnings. Asserts
/// the self-silencing effect directly instead of assuming it from the DTO change alone.
/// </summary>
public class Rpd01FieldPromotionSchemaMapTests
{
    [Fact]
    public void Extract_LiveOrder_IncludesOrderCancellationBySystemReason()
    {
        var result = DtoFieldMap.Extract(typeof(LiveOrder));

        result.FieldNames.ShouldContain("order_cancellation_by_system_reason");
    }

    [Fact]
    public void Extract_ContractSearchResult_IncludesShowPripsAndLegSecType()
    {
        var result = DtoFieldMap.Extract(typeof(ContractSearchResult));

        result.FieldNames.ShouldContain("showPrips");
        result.FieldNames.ShouldContain("legSecType");
    }

    [Fact]
    public void Extract_CancelOrderResponse_IncludesAccount()
    {
        var result = DtoFieldMap.Extract(typeof(CancelOrderResponse));

        result.FieldNames.ShouldContain("account");
    }
}
