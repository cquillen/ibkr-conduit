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
