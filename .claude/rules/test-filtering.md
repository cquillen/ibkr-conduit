## dotnet test filtering (xUnit v3 + Microsoft Testing Platform)

This repo uses xUnit v3 with Microsoft Testing Platform (MTP) configured via `global.json`. The standard VSTest `--filter` flag does NOT work. Use the MTP-native filter flags instead.

### MTP filter flags

Wildcards (`*`) supported at beginning and/or end of each filter value.

```bash
# Filter by class (wildcards required for partial match)
dotnet test --project <path> --filter-class "*WatchlistTests*"

# Filter by method
dotnet test --project <path> --filter-method "*CreateWatchlist_Success*"

# Filter by namespace
dotnet test --project <path> --filter-namespace "*Watchlists*"

# Filter by trait
dotnet test --project <path> --filter-trait "Category=Integration"

# Exclude filters (AND logic when multiple)
dotnet test --project <path> --filter-not-class "*SlowTests*"
dotnet test --project <path> --filter-not-method "*401Recovery*"

# Multiple values = OR logic
dotnet test --project <path> --filter-class "*WatchlistTests*" "*SessionTests*"
```

### Important constraints

- Cannot combine simple filters of different kinds (e.g., `--filter-class` AND `--filter-method` together)
- For complex queries, use `--filter-query` with the query language: `/assemblyName/namespace/type/method[trait=value]`
- Multiple values for the same filter flag = OR operation
- Multiple `--filter-not-*` flags = AND operation (all excluded)
- Specifying a test project must use `--project` flag, not positional path

### Common patterns for this repo

```bash
# Run all watchlist integration tests (8 tests)
dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*WatchlistTests*"

# Run a single test
dotnet test --project tests/IbkrConduit.Tests.Integration --filter-method "*CreateWatchlist_Success*"

# Run all tests in a namespace
dotnet test --project tests/IbkrConduit.Tests.Integration --filter-namespace "*Watchlists*"

# Run unit tests for a specific class
dotnet test --project tests/IbkrConduit.Tests.Unit --filter-class "*WatchlistOperationsTests*"

# Run multiple test classes (OR)
dotnet test --project tests/IbkrConduit.Tests.Integration --filter-class "*WatchlistTests*" "*SessionTests*"

# Add --no-build to skip rebuild
dotnet test --project tests/IbkrConduit.Tests.Integration --no-build --configuration Release --filter-class "*WatchlistTests*"
```

### Configuration (already set up)

- `global.json`: `"test": {"runner": "Microsoft.Testing.Platform"}`
- `Directory.Build.props` (tests): `UseMicrosoftTestingPlatformRunner=true`, `TestingPlatformDotnetTestSupport=true`
- Packages: `xunit.v3`, `xunit.runner.visualstudio`
