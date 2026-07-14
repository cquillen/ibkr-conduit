using IbkrConduit.Http;
using IbkrConduit.Orders;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Http;

/// <summary>
/// RPD-02: proves the newly-promoted <see cref="LiveOrder.ParentId"/> and
/// <see cref="LiveOrder.OcaGroupId"/> fields are recognized by <see cref="DtoFieldMap"/> — the
/// mechanism <c>ResponseSchemaValidationHandler</c> uses to flag "Extra fields" warnings. Asserts
/// the self-silencing effect directly rather than assuming it from the DTO change alone; fails red
/// while the fields still surface only through <c>AdditionalData</c>.
/// </summary>
public class Rpd02FieldPromotionSchemaMapTests
{
    [Fact]
    public void Extract_LiveOrder_IncludesParentId()
    {
        var result = DtoFieldMap.Extract(typeof(LiveOrder));

        result.FieldNames.ShouldContain("parentId");
    }

    [Fact]
    public void Extract_LiveOrder_IncludesOcaGroupId()
    {
        var result = DtoFieldMap.Extract(typeof(LiveOrder));

        result.FieldNames.ShouldContain("ocaGroupId");
    }
}
