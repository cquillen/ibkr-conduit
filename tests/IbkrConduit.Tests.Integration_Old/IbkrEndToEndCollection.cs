namespace IbkrConduit.Tests.Integration;

/// <summary>
/// Collection definition that serializes all E2E tests hitting real IBKR.
/// Only one brokerage session can be active at a time per username,
/// so E2E tests must not run in parallel.
/// </summary>
[CollectionDefinition("IBKR E2E")]
public class IbkrEndToEndCollection;
