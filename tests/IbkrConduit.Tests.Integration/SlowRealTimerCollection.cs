using Xunit;

namespace IbkrConduit.Tests.Integration;

/// <summary>
/// Collection definition that isolates the Slow real-timer integration tests
/// (tickle-timer, WebSocket reconnect watchdog, REST resilience) from the rest of
/// the parallel integration suite.
/// </summary>
/// <remarks>
/// These tests drive real wall-clock timers (short tickle/heartbeat/retry cadences)
/// for several seconds each. When they run concurrently with the large parallel
/// integration set they starve CPU and destabilize timing-sensitive tests (e.g. the
/// transport-fault resilience assertions) — an intermittent failure observed only in
/// the combined full run (which includes <c>Category=Slow</c>), never in CI (which
/// excludes Slow) nor when these classes run in isolation. <see cref="Xunit.CollectionDefinitionAttribute.DisableParallelization"/>
/// runs this collection serially and not concurrently with the parallelizable
/// collections, removing the contention so the single full <c>dotnet test</c> run is
/// stable. The classes are tagged <c>[Trait("Category","Slow")]</c> so CI still skips
/// them via <c>--filter-not-trait "Category=Slow"</c>.
/// </remarks>
[CollectionDefinition("Slow real-timer", DisableParallelization = true)]
public class SlowRealTimerCollection;
