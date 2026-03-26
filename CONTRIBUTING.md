# Contributing to IbkrConduit

Thank you for your interest in contributing!

## Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) (LTS)
- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (current)
- Git

## Getting Started

```bash
git clone https://github.com/cquillen/ibkr-conduit.git
cd ibkr-conduit
dotnet restore
dotnet build
dotnet test
```

## Development Workflow

1. Create a feature branch from `main`
2. Make your changes
3. Ensure all checks pass locally:
   - `dotnet build --configuration Release` (zero warnings)
   - `dotnet test --configuration Release`
   - `dotnet format --verify-no-changes`
4. Commit using [Conventional Commits](https://www.conventionalcommits.org/) format:
   - `feat: add new feature`
   - `fix: resolve bug`
   - `docs: update documentation`
   - `chore: maintenance task`
   - `refactor: code improvement`
   - `test: add or update tests`
   - `ci: CI/CD changes`
5. Open a pull request against `main`
6. **The CI pipeline must pass before merge** — build, lint, and tests are required status checks

## Code Style

- Follow the `.editorconfig` rules — they are enforced in CI via `dotnet format`
- Use `var` for all variable declarations
- Use file-scoped namespaces
- Add XML documentation comments on all public APIs
- Zero compiler warnings — `TreatWarningsAsErrors` is enabled

## Testing

- **Unit tests:** xUnit v3 + Shouldly assertions. No network, no file I/O.
- **Integration tests:** xUnit v3 + Shouldly + WireMock.Net. No real IBKR connectivity required.
- Test naming convention: `MethodName_Scenario_ExpectedResult`

## Adding a New Endpoint

1. Add the method to the appropriate Refit interface in `src/IbkrConduit/`
2. Add request/response model types
3. Add unit tests
4. Add WireMock integration test with recorded or hand-crafted cassette

## Security

Please report security vulnerabilities through GitHub's private vulnerability reporting or see [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
