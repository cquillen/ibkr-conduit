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
