# IbkrConduit — Repository Scaffolding Design

**Date:** 2026-03-26
**Status:** Approved
**Scope:** Repo initialization, project structure, CI/CD, open source governance, developer tooling. No feature implementation.

---

## 1. Solution & Project Structure

### 1.1 Layout

```
ibkr-conduit/
├── src/
│   └── IbkrConduit/
│       └── IbkrConduit.csproj
├── tests/
│   ├── IbkrConduit.Tests.Unit/
│   │   └── IbkrConduit.Tests.Unit.csproj
│   └── IbkrConduit.Tests.Integration/
│       └── IbkrConduit.Tests.Integration.csproj
├── docs/
│   ├── ibkr_conduit_design.md      # moved from repo root
│   ├── implementation-status.md    # tracks what's implemented vs not
│   └── superpowers/
│       └── specs/
├── .github/
│   ├── workflows/
│   │   ├── ci.yml
│   │   ├── publish.yml
│   │   └── changelog.yml
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md
│   │   ├── feature_request.md
│   │   └── config.yml
│   ├── pull_request_template.md
│   ├── CODEOWNERS
│   └── FUNDING.yml          (not included — skip for now)
├── .claude/
│   ├── settings.json
│   └── rules/
│       ├── code-style.md
│       ├── build-quality.md
│       ├── git-conventions.md
│       ├── architecture.md
│       ├── testing.md
│       └── security.md
├── IbkrConduit.slnx
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
├── .gitignore
├── .gitattributes
├── CLAUDE.md
├── README.md
├── CONTRIBUTING.md
├── CHANGELOG.md
├── SECURITY.md
├── CODE_OF_CONDUCT.md
└── LICENSE
```

### 1.2 Solution Format

Use the modern `.slnx` XML-based solution format. References the three projects: `src/IbkrConduit`, `tests/IbkrConduit.Tests.Unit`, `tests/IbkrConduit.Tests.Integration`.

### 1.3 Target Frameworks

The library targets `net8.0` and `net10.0` (dual TFM). Test projects target `net10.0` only.

- `net8.0` — LTS, supported until November 2026, widens consumer audience
- `net10.0` — current stable release

### 1.4 Directory.Build.props

Shared build properties applied to all projects in the repo:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
</Project>
```

- **Zero warnings enforced** — `TreatWarningsAsErrors` turns any warning into a build failure
- **Code style enforced in build** — `.editorconfig` rules checked at build time, not just in IDE
- **Latest analyzers** — `AnalysisLevel` at recommended severity

### 1.5 Directory.Packages.props

Central package version management. All NuGet package versions are defined here. Individual `.csproj` files reference packages with `<PackageReference Include="..." />` without `Version` attributes.

### 1.6 Main Library Project (IbkrConduit.csproj)

- Class library targeting `net8.0;net10.0`
- NuGet package metadata from design doc section 17.3:
  - `PackageId`: `IbkrConduit`
  - `Version`: `0.1.0`
  - `Authors`: `Robert Craig Quillen`
  - `PackageLicenseExpression`: `MIT`
  - `PackageTags`: `ibkr;interactive-brokers;trading;oauth;client-portal-api;algorithmic-trading`
  - `PackageProjectUrl` / `RepositoryUrl`: `https://github.com/cquillen/ibkr-conduit`
  - `PackageReadmeFile`: `README.md`
  - `GenerateDocumentationFile`: `true`
  - `Description`: includes financial disclaimer
- No internal folder structure yet — emerges as code is written
- Stub: single empty `Class1.cs` or equivalent placeholder that compiles cleanly

### 1.7 Unit Test Project (IbkrConduit.Tests.Unit.csproj)

- Targets `net10.0`
- References: xUnit, xUnit runner, Shouldly, coverlet for coverage
- Project reference to `src/IbkrConduit`
- Stub: single placeholder test file that passes

### 1.8 Integration Test Project (IbkrConduit.Tests.Integration.csproj)

- Targets `net10.0`
- References: xUnit, xUnit runner, Shouldly, coverlet, WireMock.Net
- Project reference to `src/IbkrConduit`
- Stub: single placeholder test file that passes

---

## 2. CI/CD Pipelines

### 2.1 CI Pipeline (`.github/workflows/ci.yml`)

Triggers on:
- `push` to `main`
- `pull_request` targeting `main`

Job: `build-and-test`

Steps:
1. `actions/checkout@v4`
2. `actions/setup-dotnet@v4` with `8.x` and `10.x` SDKs
3. `dotnet restore`
4. `dotnet format --verify-no-changes` — lint check against `.editorconfig`
5. `dotnet build --no-restore --configuration Release` — zero warnings enforced
6. `dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage"`
7. `codecov/codecov-action@v4` — upload coverage

### 2.2 Publish Pipeline (`.github/workflows/publish.yml`)

Triggers on tags matching `v*.*.*`.

Steps:
1. `actions/checkout@v4`
2. `actions/setup-dotnet@v4` with `10.x`
3. Extract version from tag (strip `v` prefix)
4. `dotnet pack src/IbkrConduit -p:PackageVersion={version} --configuration Release --output ./artifacts`
5. `dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json`

Inert until `NUGET_API_KEY` secret is configured.

### 2.3 Changelog Pipeline (`.github/workflows/changelog.yml`)

Triggers on tags matching `v*.*.*`.

Uses a GitHub Action to auto-generate release notes from conventional commit messages since the last tag. Creates or updates the GitHub Release with generated notes.

---

## 3. `.gitignore`

Comprehensive coverage for:

**Build outputs:**
- `bin/`, `obj/`, `artifacts/`, `publish/`, `out/`
- `*.nupkg`, `*.snupkg`

**.NET / C# specific:**
- `*.user`, `*.suo`, `*.cache`
- `project.lock.json`, `*.nuget.props`, `*.nuget.targets`
- `TestResults/`, `coverage/`

**Visual Studio:**
- `.vs/`, `*.userprefs`, `*.sln.docstates`

**JetBrains Rider:**
- `.idea/`, `*.sln.iml`

**VS Code and forks (Windsurf, Cursor, etc.):**
- `.vscode/`
- `.windsurf/`, `.cursor/`

**OS files:**
- `Thumbs.db`, `Desktop.ini`, `.DS_Store`

**Security / secrets:**
- `*.pem`, `*.pfx`, `*.key`
- `appsettings.*.json` local overrides
- `.env`

---

## 4. `.gitattributes`

Enforce LF line endings everywhere:

```
* text=auto eol=lf
*.cs text eol=lf
*.csproj text eol=lf
*.props text eol=lf
*.slnx text eol=lf
*.json text eol=lf
*.yml text eol=lf
*.yaml text eol=lf
*.md text eol=lf
*.xml text eol=lf
*.png binary
*.jpg binary
*.ico binary
```

---

## 5. `.editorconfig`

### 5.1 General

- UTF-8 encoding
- LF line endings
- Trim trailing whitespace
- Insert final newline
- 4-space indentation

### 5.2 C# Style Rules

- `var` everywhere (`csharp_style_var_for_built_in_types = true`, `csharp_style_var_when_type_is_apparent = true`, `csharp_style_var_elsewhere = true`)
- File-scoped namespaces enforced
- Expression-bodied members preferred where single-line
- `this.` qualification: never
- Braces required for all control flow
- Sort `System` usings first

### 5.3 Naming Conventions

- `PascalCase` — public members, types, methods
- `_camelCase` — private fields (underscore prefix)
- `camelCase` — local variables, parameters
- `IPascalCase` — interfaces
- `TPascalCase` — type parameters

### 5.4 Formatting

- 4-space indentation
- Allman-style braces (each on own line)
- One class per file encouraged

### 5.5 Severity

All style rules set to `warning` severity. Combined with `EnforceCodeStyleInBuild` and `TreatWarningsAsErrors`, these become build errors.

---

## 6. Open Source Documents

### 6.1 LICENSE

MIT license. Copyright holder: `Robert Craig Quillen`. Standard MIT text — not modified.

### 6.2 README.md

Structure per design doc section 17.10:

1. Badge row — NuGet version, CI status (GitHub Actions), coverage (Codecov), license
2. One-paragraph description
3. **Legal disclaimer** — financial trading risk warning, not affiliated with Interactive Brokers, infrastructure software only, not responsible for trading decisions or financial outcomes, test with paper account first
4. Quick start — `dotnet add package IbkrConduit`, placeholder DI registration example
5. Features overview — list from design doc
6. Authentication setup — placeholder, link to future docs
7. IbkrConduit vs IBKR.Sdk.Client comparison table
8. Contributing — link to `CONTRIBUTING.md`
9. License — link to `LICENSE`

### 6.3 CONTRIBUTING.md

- Prerequisites: .NET SDK 8 and 10
- Building: `dotnet build`
- Running tests: `dotnet test`
- Linting: `dotnet format --verify-no-changes`
- Integration tests use WireMock — no IBKR account needed
- Code style: follow `.editorconfig`, enforced in CI
- Commit messages: Conventional Commits format
- **CI pipeline must pass before merge** — build, lint, and tests are required status checks
- How to add a new endpoint: Refit interface, model types, unit test, WireMock cassette
- Security issues: reference `SECURITY.md`

### 6.4 SECURITY.md

- Primary channel: GitHub private vulnerability reporting (enabled in repo settings)
- Fallback contact: craig@thequillens.com
- Expected response timeline: acknowledge within 48 hours, aim to resolve within 30 days
- Scope: vulnerabilities in IbkrConduit code — credential handling, signing, session management
- Out of scope: IBKR's own API security (report to IBKR directly), issues in consuming applications

### 6.5 CHANGELOG.md

Stub file with `## [Unreleased]` section. Content auto-generated from conventional commits by the changelog GitHub Action on releases.

### 6.6 CODE_OF_CONDUCT.md

Contributor Covenant v2.1 — standard text. Contact: craig@thequillens.com.

---

## 7. GitHub Templates & Configuration

### 7.1 Pull Request Template

`.github/pull_request_template.md`:
- Description section
- Type of change checkboxes: bug fix, new feature, breaking change, documentation, dependency update
- Testing checkboxes: unit tests added/updated, integration tests added/updated, tested against paper account
- Checklist: code follows style conventions, XML docs updated, CHANGELOG updated, no secrets in code

### 7.2 Bug Report Issue Template

`.github/ISSUE_TEMPLATE/bug_report.md`:
- IbkrConduit version
- .NET version
- Steps to reproduce
- Expected vs actual behavior
- Logs (with reminder to sanitize credentials)

### 7.3 Feature Request Issue Template

`.github/ISSUE_TEMPLATE/feature_request.md`:
- Problem statement
- Proposed solution
- Alternatives considered
- IBKR API documentation links if relevant

### 7.4 Issue Template Config

`.github/ISSUE_TEMPLATE/config.yml`:
- Disable blank issues
- Link to `SECURITY.md` for security reports

### 7.5 CODEOWNERS

`* @cquillen` — all files require review from repo owner.

### 7.6 Branch Protection (main)

Configured after initial commit to `main`:
- Require pull request before merging — no direct pushes
- Require at least 1 approving review
- Dismiss stale reviews when new commits are pushed
- Require status checks to pass: `build-and-test`
- Require branches to be up to date before merging
- Do not allow bypassing the above settings

---

## 8. Implementation Status Tracker

### 8.1 Purpose

`docs/implementation-status.md` tracks what has and has not been implemented from the design document. Each implementable capability is a row. Updated as part of each spec/implementation cycle.

### 8.2 Format

```markdown
# IbkrConduit — Implementation Status

Tracks implementation progress against [ibkr_conduit_design.md](ibkr_conduit_design.md).

## Status Key

- **Not Started** — no work begun
- **Spec'd** — design spec written in `docs/superpowers/specs/`
- **In Progress** — implementation underway
- **Done** — implemented and tested

## Repo Scaffolding

| Capability | Status | Spec |
|---|---|---|
| ... | ... | ... |

## Phase 1 — Core Authentication and Session

| Capability | Status | Spec |
|---|---|---|
| ... | ... | ... |

(etc. for each phase)
```

### 8.3 Sections

Organized by the design doc's implementation phases (section 18), plus a "Repo Scaffolding" section for the current spec. Each phase has its own table.

### 8.4 Rules

- Updated at the end of every implementation session
- The Spec column links to the superpowers spec that covers it
- Only mark "Done" when the capability is implemented AND tested
- `CLAUDE.md` references this file so every session knows to check it

---

## 9. Claude Code Configuration

### 9.1 CLAUDE.md (root)

Thin entry point:
- One-liner: what IbkrConduit is
- Pointer to `docs/ibkr_conduit_design.md` as authoritative design reference
- Pointer to `docs/implementation-status.md` — check at session start to know what's done and what's next
- Build commands: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`
- Pointer to `.claude/rules/` for detailed guidance

### 9.2 .claude/rules/ (scoped rule files)

**`code-style.md`** — globs: `**/*.cs`
- `var` everywhere
- File-scoped namespaces
- Nullable reference types enabled
- XML doc comments on all public APIs
- Follow `.editorconfig` — it's enforced in CI
- LF line endings

**`build-quality.md`** — globs: `**/*.csproj`, `**/Directory.Build.props`, `**/Directory.Packages.props`
- Zero warnings — `TreatWarningsAsErrors` is on
- Do not add `<NoWarn>` without explicit justification in a comment
- All NuGet package versions in `Directory.Packages.props` only — never version attributes in individual `.csproj` files

**`git-conventions.md`** — always active
- Conventional Commits: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`, `test:`, `ci:`
- GitHub Flow — feature branches off `main`, PRs to merge
- CI must be green before merge
- Never commit secrets, `.pem` files, or credentials

**`architecture.md`** — globs: `src/**`
- Multi-tenant by design — no global/static state
- Storage-agnostic — credentials are pre-loaded objects, not file paths
- All crypto via `System.Security.Cryptography` — no external crypto libraries
- Refer to `docs/ibkr_conduit_design.md` for detailed design decisions

**`testing.md`** — globs: `tests/**`
- xUnit for all tests
- Shouldly for all assertions — use `ShouldBe`, `ShouldNotBeNull`, `ShouldThrow`, etc. Do not use xUnit's built-in `Assert` class
- Unit tests: no network, no file I/O, mock external dependencies
- Integration tests: WireMock.Net for HTTP mocking, no real IBKR connectivity
- Test naming: `MethodName_Scenario_ExpectedResult`

**`security.md`** — always active
- No credentials, tokens, or key material in code or test fixtures
- No `.pem` files committed
- Log sanitization — ensure no credential material in log output
- Financial disclaimer must appear in README and NuGet description

### 9.3 .claude/settings.json

Permissive within repo scope:
- Allow: `dotnet` commands (build, test, format, pack, restore, new, sln, add, etc.)
- Allow: `git` commands
- Allow: file read/write within the repo

Anything outside the repo prompts for approval.

---

## 10. Git Initialization

### 10.1 Sequence

1. `git init` in `D:/code/ibkr-conduit/`
2. Move `ibkr_conduit_design.md` from repo root to `docs/ibkr_conduit_design.md`
3. Update target frameworks in design doc section 17.1 to `net8.0;net10.0`
4. Create all files per this spec
5. `dotnet restore` — verify solution builds
6. `dotnet build --configuration Release` — verify zero warnings
7. `dotnet test --configuration Release` — verify tests pass (placeholder tests)
8. `dotnet format --verify-no-changes` — verify lint compliance
9. Commit everything to `main`
10. Create GitHub repo (`cquillen/ibkr-conduit`)
11. Push to remote
12. Configure branch protection rules on `main`
13. Enable GitHub private vulnerability reporting

### 10.2 Branch Protection Setup

After the initial push, configure via GitHub CLI (`gh`) or repo settings:
- Require PR for merges
- Require 1 review
- Require `build-and-test` status check
- Dismiss stale reviews
- Require branch to be up to date
- No bypass
