using System;
using System.Net;
using IbkrConduit.Errors;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Errors;

public class ResultTests
{
    [Fact]
    public void Success_IsSuccess_ReturnsTrue()
    {
        var result = Result<string>.Success("hello");
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("hello");
    }

    [Fact]
    public void Failure_IsSuccess_ReturnsFalse()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<string>.Failure(error);
        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Success_AccessingError_Throws()
    {
        var result = Result<string>.Success("hello");
        Should.Throw<InvalidOperationException>(() => { _ = result.Error; });
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<string>.Failure(error);
        Should.Throw<InvalidOperationException>(() => { _ = result.Value; });
    }

    [Fact]
    public void EnsureSuccess_OnSuccess_ReturnsSelf()
    {
        var result = Result<string>.Success("hello");
        var returned = result.EnsureSuccess();
        returned.Value.ShouldBe("hello");
    }

    [Fact]
    public void EnsureSuccess_OnFailure_ThrowsIbkrApiException()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<string>.Failure(error);
        var ex = Should.Throw<IbkrApiException>(() => result.EnsureSuccess());
        ex.Error.ShouldBe(error);
    }

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        var result = Result<int>.Success(42);
        var mapped = result.Map(v => v.ToString());
        mapped.IsSuccess.ShouldBeTrue();
        mapped.Value.ShouldBe("42");
    }

    [Fact]
    public void Map_OnFailure_PreservesError()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<int>.Failure(error);
        var mapped = result.Map(v => v.ToString());
        mapped.IsSuccess.ShouldBeFalse();
        mapped.Error.ShouldBe(error);
    }

    [Fact]
    public void Match_OnSuccess_CallsSuccessFunc()
    {
        var result = Result<int>.Success(42);
        var output = result.Match(v => $"ok:{v}", e => $"err:{e.Message}");
        output.ShouldBe("ok:42");
    }

    [Fact]
    public void Match_OnFailure_CallsErrorFunc()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<int>.Failure(error);
        var output = result.Match(v => $"ok:{v}", e => $"err:{e.Message}");
        output.ShouldBe("err:bad");
    }

    [Fact]
    public void Switch_OnSuccess_CallsSuccessAction()
    {
        var result = Result<int>.Success(42);
        var called = false;
        result.Switch(v => called = true, e => { });
        called.ShouldBeTrue();
    }

    [Fact]
    public void Switch_OnFailure_CallsErrorAction()
    {
        var error = new IbkrApiError(HttpStatusCode.BadRequest, "bad", "", "/test");
        var result = Result<int>.Failure(error);
        var called = false;
        result.Switch(v => { }, e => called = true);
        called.ShouldBeTrue();
    }
}
