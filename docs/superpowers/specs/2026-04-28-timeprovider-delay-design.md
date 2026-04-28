# TimeProvider Delay Abstraction — Design Spec

**Date:** 2026-04-28
**Status:** Approved

## Problem

Two unit test classes use real `Task.Delay` calls, making the unit suite artificially slow:

| Class | Root cause | Approx cost |
|-------|-----------|------------|
| `TickleTimerTests` | Real `PeriodicTimer` at 1s intervals; tests `await Task.Delay(2500ms)` to observe ticks | ~10s |
| `FlexOperationsPollingTests` | Real `Task.Delay` for poll backoff (1–5s) and rate-limit waits (10s) | ~4s |

## Goal

Inject `System.TimeProvider` into `TickleTimer` and `FlexOperations` so tests can use `FakeTimeProvider.Advance()` to drive time forward instantly, eliminating all real waiting from these test classes. Add a `TimeProviderExtensions.Delay()` method to give `TimeProvider` async delay semantics that compose with `FakeTimeProvider`.

## Non-Goals

- Abstracting clock access (`GetUtcNow`) — neither component needs it
- Replacing `Stopwatch` usage in `FlexOperations` diagnostics
- Changing `SessionManager`, `MarketDataOperations`, or other components with similar patterns (follow-on work if desired)

## Design

### `TimeProviderExtensions`

New file: `src/IbkrConduit/TimeProviderExtensions.cs`

```csharp
internal static class TimeProviderExtensions
{
    internal static Task Delay(this TimeProvider timeProvider, int milliseconds, CancellationToken cancellationToken = default)
        => timeProvider.Delay(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);

    internal static Task Delay(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        var timer = timeProvider.CreateTimer(
            _ => { tcs.TrySetResult(); registration.Dispose(); },
            null, delay, Timeout.InfiniteTimeSpan);
        tcs.Task.ContinueWith(_ => timer.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return tcs.Task;
    }
}
```

`FakeTimeProvider.Advance()` fires the underlying `ITimer`, which calls the callback, which completes the `TaskCompletionSource`. No real time passes.

### `TickleTimer` changes

- Add `TimeProvider timeProvider` parameter to the constructor
- Replace the `PeriodicTimer` loop:
  ```csharp
  // BEFORE
  using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_intervalSeconds));
  while (await timer.WaitForNextTickAsync(cancellationToken)) { ... }

  // AFTER
  while (!cancellationToken.IsCancellationRequested)
  {
      await _timeProvider.Delay(_intervalSeconds * 1000, cancellationToken);
      // tickle logic
  }
  ```
- Store `TimeProvider` as `private readonly TimeProvider _timeProvider`

### `FlexOperations` changes

- Add `TimeProvider timeProvider` parameter to the constructor
- Replace both `Task.Delay(...)` call sites with `_timeProvider.Delay(...)`
- Store `TimeProvider` as `private readonly TimeProvider _timeProvider`

### DI registration

In `IbkrClientServiceCollectionExtensions` (or wherever `TickleTimer` and `FlexOperations` are registered), pass `TimeProvider.System` for the `timeProvider` parameter. `TimeProvider.System` is a singleton provided by the BCL — no need to register it in the DI container.

### Test package

Add `Microsoft.Extensions.TimeProvider.Testing` to `tests/IbkrConduit.Tests.Unit` only (test project, not production).

### `TickleTimerTests` changes

Replace patterns like:
```csharp
var timer = new TickleTimer(..., intervalSeconds: 1);
await Task.Delay(2500, ct); // wait for 2 ticks
```

With:
```csharp
var fakeTime = new FakeTimeProvider();
var timer = new TickleTimer(..., intervalSeconds: 1, fakeTime);
// advance time to trigger ticks
fakeTime.Advance(TimeSpan.FromSeconds(1)); // tick 1
fakeTime.Advance(TimeSpan.FromSeconds(1)); // tick 2
```

### `FlexOperationsPollingTests` changes

Replace real delay waits with `fakeTime.Advance(...)` calls sized to the delay being exercised (e.g. `Advance(TimeSpan.FromSeconds(10))` for the rate-limit delay, `Advance(TimeSpan.FromSeconds(1))` for the first poll delay).

## Files Changed

| Action | Path |
|--------|------|
| Create | `src/IbkrConduit/TimeProviderExtensions.cs` |
| Modify | `src/IbkrConduit/Session/TickleTimer.cs` |
| Modify | `src/IbkrConduit/Client/FlexOperations.cs` |
| Modify | DI registration file (wherever TickleTimer/FlexOperations are wired up) |
| Modify | `tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj` |
| Modify | `tests/IbkrConduit.Tests.Unit/Session/TickleTimerTests.cs` |
| Modify | `tests/IbkrConduit.Tests.Unit/Flex/FlexOperationsPollingTests.cs` |

## Expected Outcome

- All tests continue to pass with identical behavior
- `TickleTimerTests` and `FlexOperationsPollingTests` run in milliseconds instead of seconds
- Unit suite runtime drops from ~49s to under 10s
- No public API changes — `TimeProvider` injection is internal constructor plumbing
