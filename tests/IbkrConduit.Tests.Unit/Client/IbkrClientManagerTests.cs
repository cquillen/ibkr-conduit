using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using IbkrConduit.Auth;
using IbkrConduit.Client;
using IbkrConduit.Http;
using IbkrConduit.Session;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Client;

public class IbkrClientManagerTests
{
    private static IbkrOAuthCredentials Creds(string tenant) =>
        new(tenant, "CONSUMERK", "tok", "sec", RSA.Create(2048), RSA.Create(2048), BigInteger.One);

    private static IbkrClientManager NewManager(ITenantBuilder builder) =>
        new(builder, new IbkrClientOptions(), new NoOpSharedRateGovernor());

    [Fact]
    public async Task AddAsync_NewTenant_IsRetrievable()
    {
        var builder = new FakeTenantBuilder();
        await using var mgr = NewManager(builder);
        var client = await mgr.AddAsync("t1", Creds("t1"), cancellationToken: TestContext.Current.CancellationToken);
        client.ShouldNotBeNull();
        mgr.TryGetClient("t1", out _).ShouldBeTrue();
        mgr.ActiveTenants.ShouldBe(new[] { "t1" });
    }

    [Fact]
    public async Task AddAsync_DuplicateTenant_Throws()
    {
        var builder = new FakeTenantBuilder();
        await using var mgr = NewManager(builder);
        await mgr.AddAsync("t1", Creds("t1"), cancellationToken: TestContext.Current.CancellationToken);
        await Should.ThrowAsync<InvalidOperationException>(
            () => mgr.AddAsync("t1", Creds("t1"), cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAsync_BuildFails_DisposesCredsAndRegistersNothing()
    {
        var builder = new FakeTenantBuilder { ThrowOnBuild = true };
        await using var mgr = NewManager(builder);
        var creds = Creds("t1");
        await Should.ThrowAsync<InvalidOperationException>(
            () => mgr.AddAsync("t1", creds, cancellationToken: TestContext.Current.CancellationToken));
        mgr.ActiveTenants.ShouldBeEmpty();
        Should.Throw<ObjectDisposedException>(() => creds.SignaturePrivateKey.ExportParameters(true));
    }

    [Fact]
    public async Task RemoveAsync_PresentTenant_TearsDownAndReturnsTrue()
    {
        var builder = new FakeTenantBuilder();
        await using var mgr = NewManager(builder);
        await mgr.AddAsync("t1", Creds("t1"), cancellationToken: TestContext.Current.CancellationToken);
        (await mgr.RemoveAsync("t1", TestContext.Current.CancellationToken)).ShouldBeTrue();
        mgr.TryGetClient("t1", out _).ShouldBeFalse();
        builder.LastTenant!.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveAsync_AbsentTenant_ReturnsFalse() =>
        (await NewManager(new FakeTenantBuilder()).RemoveAsync("nope", TestContext.Current.CancellationToken))
            .ShouldBeFalse();

    [Fact]
    public void GetClient_Absent_Throws() =>
        Should.Throw<KeyNotFoundException>(() => NewManager(new FakeTenantBuilder()).GetClient("nope"));

    [Fact]
    public async Task DisposeAsync_TearsDownAllTenants()
    {
        var builder = new FakeTenantBuilder();
        var mgr = NewManager(builder);
        await mgr.AddAsync("t1", Creds("t1"), cancellationToken: TestContext.Current.CancellationToken);
        await mgr.AddAsync("t2", Creds("t2"), cancellationToken: TestContext.Current.CancellationToken);
        await mgr.DisposeAsync();
        builder.Built.ShouldAllBe(t => t.Disposed);
    }

    [Fact]
    public async Task AddAsync_ConcurrentDuplicates_OnlyOneWinsAndBuildsOnce()
    {
        var builder = new FakeTenantBuilder();
        await using var mgr = NewManager(builder);
        var ct = TestContext.Current.CancellationToken;

        // Race 20 adds for the same id across the thread pool; only one may reserve the slot.
        var adds = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => mgr.AddAsync("t1", Creds("t1"), cancellationToken: ct), ct))
            .ToArray();

        var outcomes = await Task.WhenAll(adds.Select(async add =>
        {
            try
            {
                await add;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }));

        outcomes.Count(won => won).ShouldBe(1);
        outcomes.Count(won => !won).ShouldBe(19);
        builder.Built.Count.ShouldBe(1);              // losers fail at the reservation, before building
        builder.LastTenant!.Disposed.ShouldBeFalse(); // the winner stays live
        mgr.ActiveTenants.ShouldBe(new[] { "t1" });
    }

    [Fact]
    public async Task AddAsync_RemovedDuringBuild_TearsDownAndDoesNotResurrect()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = new FakeTenantBuilder { Gate = gate };
        await using var mgr = NewManager(builder);
        var ct = TestContext.Current.CancellationToken;

        // Start the add; its build suspends at the gate with only a reservation in the slot.
        var add = mgr.AddAsync("t1", Creds("t1"), cancellationToken: ct);

        // Remove wins the slot mid-build: it yanks the reservation and reports not-active.
        (await mgr.RemoveAsync("t1", ct)).ShouldBeFalse();

        gate.SetResult();

        // The completing build must not resurrect the removed tenant — it tears down and throws.
        await Should.ThrowAsync<InvalidOperationException>(() => add);
        builder.Built.ShouldHaveSingleItem().Disposed.ShouldBeTrue();
        mgr.ActiveTenants.ShouldBeEmpty();
    }
}
