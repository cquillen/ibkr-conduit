---
description: Testing conventions for IbkrConduit
globs: "tests/**"
---

- Use xUnit v3 for all tests
- Use Shouldly for all assertions — `ShouldBe`, `ShouldNotBeNull`, `ShouldThrow`, etc.
- Do NOT use xUnit's built-in `Assert` class — use Shouldly exclusively
- Test naming convention: `MethodName_Scenario_ExpectedResult`
- Unit tests (`Tests.Unit`): no network calls, no file I/O, mock all external dependencies
- Integration tests (`Tests.Integration`): use WireMock.Net for HTTP mocking, no real IBKR connectivity required
- Integration tests use the full DI stack via `AddIbkrClient` with `IbkrClientOptions.BaseUrl` pointed at WireMock — no fakes, no mocked services
- Every integration-tested endpoint MUST include a 401 recovery test that verifies: first call returns 401, TokenRefreshHandler triggers re-auth (new LST + ssodh/init), original request is retried and succeeds
- Each test should test one behavior — keep tests focused and independent
- E2E tests use `[EnvironmentFact("IBKR_CONSUMER_KEY")]` — runs when env var is set, skips when missing
- E2E tests MUST use the DI pipeline exactly as a real consumer would — no manual `HttpClient` wiring, no `RestService.For<>`, no direct `SessionManager` instantiation:
  ```csharp
  using var creds = OAuthCredentialsFactory.FromEnvironment();
  var services = new ServiceCollection();
  services.AddLogging();
  services.AddIbkrClient(creds);
  await using var provider = services.BuildServiceProvider();
  var client = provider.GetRequiredService<IIbkrClient>();
  ```
- All integration test classes with E2E tests must have `[Collection("IBKR E2E")]` to prevent parallel session competition
- Use `[ExcludeFromCodeCoverage]` on trivial code (record types, pure pass-through operations, static constant classes) to keep coverage metrics meaningful
- Do NOT use `[ExcludeFromCodeCoverage]` on code with branching logic, error handling, state management, or protocol flows
- When modifying code that has `[ExcludeFromCodeCoverage]`, reassess whether it is still trivial — if the change adds branching logic or non-trivial behavior, remove the exclusion and write tests for it
