using System;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class IbkrTransientExceptionTests
{
    [Fact]
    public void Constructor_WithMessageAndInnerException_StoresBoth()
    {
        var inner = new InvalidOperationException("network");

        var ex = new IbkrTransientException("boom", inner);

        ex.Message.ShouldBe("boom");
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void Constructor_WithMessageOnly_StoresMessageAndNoInner()
    {
        var ex = new IbkrTransientException("boom");

        ex.Message.ShouldBe("boom");
        ex.InnerException.ShouldBeNull();
    }
}
