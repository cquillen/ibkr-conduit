using IbkrConduit.Health;
using Shouldly;

namespace IbkrConduit.Tests.Unit.Health;

public sealed class SessionHealthStateTests
{
    [Fact]
    public void Update_SetsAllPropertiesAtomically()
    {
        var state = new SessionHealthState();

        state.Update(authenticated: true, connected: true, competing: false, established: true, failReason: null);

        state.Authenticated.ShouldBeTrue();
        state.Connected.ShouldBeTrue();
        state.Competing.ShouldBeFalse();
        state.Established.ShouldBeTrue();
        state.FailReason.ShouldBeNull();
    }

    [Fact]
    public void Update_WithFailReason_SetsAllProperties()
    {
        var state = new SessionHealthState();

        state.Update(authenticated: false, connected: false, competing: true, established: false, failReason: "network error");

        state.Authenticated.ShouldBeFalse();
        state.Connected.ShouldBeFalse();
        state.Competing.ShouldBeTrue();
        state.Established.ShouldBeFalse();
        state.FailReason.ShouldBe("network error");
    }

    [Fact]
    public void SetFailed_SetsReasonAndClearsAuthenticated()
    {
        var state = new SessionHealthState();
        state.Update(authenticated: true, connected: true, competing: false, established: true);

        state.SetFailed("token expired");

        state.Authenticated.ShouldBeFalse();
        state.FailReason.ShouldBe("token expired");
    }

    [Fact]
    public void GetSnapshot_ReturnsConsistentCopy()
    {
        var state = new SessionHealthState();
        state.Update(authenticated: true, connected: true, competing: false, established: true, failReason: null);

        var snapshot = state.GetSnapshot();

        snapshot.Authenticated.ShouldBeTrue();
        snapshot.Connected.ShouldBeTrue();
        snapshot.Competing.ShouldBeFalse();
        snapshot.Established.ShouldBeTrue();
        snapshot.FailReason.ShouldBeNull();
    }

    [Fact]
    public void GetSnapshot_IsNotAffectedBySubsequentUpdates()
    {
        var state = new SessionHealthState();
        state.Update(authenticated: true, connected: true, competing: false, established: true);

        var snapshot = state.GetSnapshot();

        state.Update(authenticated: false, connected: false, competing: true, established: false, failReason: "failed");

        snapshot.Authenticated.ShouldBeTrue();
        snapshot.Connected.ShouldBeTrue();
        snapshot.Competing.ShouldBeFalse();
        snapshot.Established.ShouldBeTrue();
        snapshot.FailReason.ShouldBeNull();
    }

    [Fact]
    public void DefaultState_AllFalseWithNoFailReason()
    {
        var state = new SessionHealthState();

        state.Authenticated.ShouldBeFalse();
        state.Connected.ShouldBeFalse();
        state.Competing.ShouldBeFalse();
        state.Established.ShouldBeFalse();
        state.FailReason.ShouldBeNull();
    }
}
