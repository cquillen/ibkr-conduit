using IbkrConduit.Orders;
using IbkrConduit.Serialization;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Serialization;

/// <summary>
/// Pins that query parameters serialize in IBKR's documented wire format. The critical case is
/// <see cref="bool"/>: the live-orders <c>force=true</c> cache-clear (§10.6, findings GAP1-2) must hit
/// the wire as lowercase <c>true</c>, but Refit's default formatter emits <c>True</c>. All other types
/// (numbers, <c>[EnumMember]</c> enums like <see cref="OrderStatusFilter"/>) must defer unchanged.
/// </summary>
public class IbkrUrlParameterFormatterTests
{
    private readonly IbkrUrlParameterFormatter _formatter = new();

    [Fact]
    public void Format_BoolTrue_SerializesLowercaseTrue()
    {
        _formatter.Format(true, typeof(object), typeof(object)).ShouldBe("true");
    }

    [Fact]
    public void Format_BoolFalse_SerializesLowercaseFalse()
    {
        _formatter.Format(false, typeof(object), typeof(object)).ShouldBe("false");
    }

    [Fact]
    public void Format_Integer_DefersToDefaultFormatter()
    {
        _formatter.Format(42, typeof(object), typeof(object)).ShouldBe("42");
    }

    [Fact]
    public void Format_EnumMemberValue_DefersToDefaultFormatter()
    {
        _formatter.Format(OrderStatusFilter.Cancelled, typeof(object), typeof(object)).ShouldBe("cancelled");
    }
}
