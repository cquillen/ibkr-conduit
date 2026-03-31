---
description: C# code style conventions for IbkrConduit
globs: "**/*.cs"
---

- Use `var` for all variable declarations — no explicit types
- Use file-scoped namespaces (`namespace Foo;` not `namespace Foo { }`)
- Nullable reference types are enabled — respect nullability annotations
- Add XML documentation comments (`///`) on all public types, methods, and properties
- Follow `.editorconfig` rules — they are enforced in CI via `dotnet format`
- Use LF line endings (enforced by `.gitattributes`)
- Prefer expression-bodied members for single-line implementations
- Always use braces for control flow statements (no single-line `if` without braces)
- `this.` qualification is unnecessary — do not use it
- Always pass `CancellationToken` through the entire call chain — every async method that accepts a `CancellationToken` must pass it to all awaited async calls, including Refit interface methods, `HttpClient` calls, `SemaphoreSlim.WaitAsync`, and `Task.Delay`
- All Refit interface methods must include `CancellationToken cancellationToken = default` as the last parameter
