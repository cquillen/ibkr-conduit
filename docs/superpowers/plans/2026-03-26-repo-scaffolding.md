# Repository Scaffolding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Initialize the ibkr-conduit git repo with full project structure, CI/CD, open source governance, and developer tooling — building and passing CI from day one with no feature code.

**Architecture:** Monorepo with a single .NET solution containing a class library (dual TFM net8.0/net10.0) and two test projects (net10.0). GitHub Actions for CI/CD. Claude Code rules for AI-assisted development. Implementation tracker for cross-session progress visibility.

**Tech Stack:** .NET 10 SDK, C# with nullable reference types, xUnit v3, Shouldly, WireMock.Net 2.0, coverlet, GitHub Actions, EditorConfig, conventional commits

**Spec:** `docs/superpowers/specs/2026-03-26-repo-scaffolding-design.md`

---

## File Map

| File | Responsibility |
|---|---|
| `.gitignore` | Ignore build outputs, IDE files, OS junk, secrets |
| `.gitattributes` | Enforce LF line endings |
| `.editorconfig` | C# code style rules (enforced in build + CI) |
| `Directory.Build.props` | Shared build properties: nullable, warnings-as-errors, analyzers |
| `Directory.Packages.props` | Central NuGet package version management |
| `IbkrConduit.slnx` | Solution file referencing all projects |
| `src/IbkrConduit/IbkrConduit.csproj` | Main library — class lib, net8.0;net10.0, NuGet metadata |
| `src/IbkrConduit/IbkrConduit.cs` | Stub public class so the library compiles |
| `tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj` | Unit test project — xUnit v3, Shouldly, coverlet |
| `tests/IbkrConduit.Tests.Unit/SmokeTests.cs` | Placeholder passing test |
| `tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj` | Integration test project — xUnit v3, Shouldly, WireMock.Net, coverlet |
| `tests/IbkrConduit.Tests.Integration/SmokeTests.cs` | Placeholder passing test |
| `LICENSE` | MIT license, copyright Robert Craig Quillen |
| `README.md` | Project description, badges, disclaimer, quick start, comparison table |
| `CONTRIBUTING.md` | Prerequisites, build/test/lint, commit format, CI requirements |
| `SECURITY.md` | Vulnerability reporting: GitHub private reporting + email fallback |
| `CHANGELOG.md` | Stub with [Unreleased] section |
| `CODE_OF_CONDUCT.md` | Contributor Covenant v2.1 |
| `cliff.toml` | git-cliff config for conventional commit changelog generation |
| `.github/workflows/ci.yml` | Build, lint, test on PR/push to main |
| `.github/workflows/publish.yml` | NuGet pack/push on version tags |
| `.github/workflows/changelog.yml` | Auto-generate release notes on version tags |
| `.github/pull_request_template.md` | PR template with checklists |
| `.github/ISSUE_TEMPLATE/bug_report.md` | Bug report template |
| `.github/ISSUE_TEMPLATE/feature_request.md` | Feature request template |
| `.github/ISSUE_TEMPLATE/config.yml` | Disable blank issues, link to SECURITY.md |
| `.github/CODEOWNERS` | @cquillen owns everything |
| `CLAUDE.md` | Thin entry point — project context, commands, pointers to rules |
| `.claude/settings.json` | Permissive within repo: dotnet, git, file ops allowed |
| `.claude/rules/code-style.md` | C# style: var, file-scoped namespaces, XML docs |
| `.claude/rules/build-quality.md` | Zero warnings, central package management |
| `.claude/rules/git-conventions.md` | Conventional commits, GitHub Flow |
| `.claude/rules/architecture.md` | Multi-tenant, storage-agnostic, System.Security.Cryptography only |
| `.claude/rules/testing.md` | xUnit v3 + Shouldly, no Assert class, WireMock for integration |
| `.claude/rules/security.md` | No credentials in code, log sanitization, disclaimer |
| `docs/ibkr_conduit_design.md` | Moved from repo root |
| `docs/implementation-status.md` | Tracks what's implemented vs not across all phases |

---

### Task 1: Git Init and Foundation Files

**Files:**
- Create: `.gitignore`
- Create: `.gitattributes`
- Create: `.editorconfig`

- [ ] **Step 1: Initialize git repo**

```bash
cd D:/code/ibkr-conduit
git init
git checkout -b main
```

Expected: Initialized empty Git repository

- [ ] **Step 2: Move design doc to docs/**

```bash
mkdir -p docs
mv ibkr_conduit_design.md docs/ibkr_conduit_design.md
```

- [ ] **Step 3: Update target frameworks in design doc**

In `docs/ibkr_conduit_design.md` section 17.1, change the target frameworks line from:

```
| Target frameworks | net6.0, net7.0, net8.0, net9.0 |
```

to:

```
| Target frameworks | net8.0, net10.0 |
```

- [ ] **Step 4: Create `.gitignore`**

```gitignore
# Build outputs
bin/
obj/
artifacts/
publish/
out/
*.nupkg
*.snupkg

# .NET / C#
*.user
*.suo
*.cache
project.lock.json
*.nuget.props
*.nuget.targets
TestResults/
coverage/

# Visual Studio
.vs/
*.userprefs
*.sln.docstates

# JetBrains Rider
.idea/
*.sln.iml

# VS Code and forks (Windsurf, Cursor, etc.)
.vscode/
.windsurf/
.cursor/

# OS files
Thumbs.db
Desktop.ini
.DS_Store

# Security / secrets
*.pem
*.pfx
*.key
appsettings.*.json
.env
```

- [ ] **Step 5: Create `.gitattributes`**

```
# Enforce LF line endings everywhere
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

# Binary files
*.png binary
*.jpg binary
*.ico binary
```

- [ ] **Step 6: Create `.editorconfig`**

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
trim_trailing_whitespace = true
insert_final_newline = true
indent_style = space
indent_size = 4

[*.{yml,yaml}]
indent_size = 2

[*.{json,slnx}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false

[*.cs]
# var preferences
csharp_style_var_for_built_in_types = true:warning
csharp_style_var_when_type_is_apparent = true:warning
csharp_style_var_elsewhere = true:warning

# Namespace preferences
csharp_style_namespace_declarations = file_scoped:warning

# Expression-bodied members
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_constructors = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = when_on_single_line:suggestion
csharp_style_expression_bodied_accessors = when_on_single_line:suggestion

# this. qualification
dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning
dotnet_style_qualification_for_method = false:warning
dotnet_style_qualification_for_event = false:warning

# Braces
csharp_prefer_braces = true:warning

# Using directives
dotnet_sort_system_directives_first = true
csharp_using_directive_placement = outside_namespace:warning

# Formatting
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_indent_case_contents = true

# Naming conventions

# Interfaces must begin with I
dotnet_naming_rule.interfaces_must_begin_with_i.symbols = interface_symbols
dotnet_naming_rule.interfaces_must_begin_with_i.style = begins_with_i
dotnet_naming_rule.interfaces_must_begin_with_i.severity = warning

dotnet_naming_symbols.interface_symbols.applicable_kinds = interface
dotnet_naming_symbols.interface_symbols.applicable_accessibilities = *

dotnet_naming_style.begins_with_i.required_prefix = I
dotnet_naming_style.begins_with_i.capitalization = pascal_case

# Type parameters must begin with T
dotnet_naming_rule.type_parameters_must_begin_with_t.symbols = type_parameter_symbols
dotnet_naming_rule.type_parameters_must_begin_with_t.style = begins_with_t
dotnet_naming_rule.type_parameters_must_begin_with_t.severity = warning

dotnet_naming_symbols.type_parameter_symbols.applicable_kinds = type_parameter
dotnet_naming_symbols.type_parameter_symbols.applicable_accessibilities = *

dotnet_naming_style.begins_with_t.required_prefix = T
dotnet_naming_style.begins_with_t.capitalization = pascal_case

# Private fields must be _camelCase
dotnet_naming_rule.private_fields_must_be_camel_case.symbols = private_field_symbols
dotnet_naming_rule.private_fields_must_be_camel_case.style = underscore_camel_case
dotnet_naming_rule.private_fields_must_be_camel_case.severity = warning

dotnet_naming_symbols.private_field_symbols.applicable_kinds = field
dotnet_naming_symbols.private_field_symbols.applicable_accessibilities = private, private_protected

dotnet_naming_style.underscore_camel_case.required_prefix = _
dotnet_naming_style.underscore_camel_case.capitalization = camel_case

# Public members must be PascalCase
dotnet_naming_rule.public_members_must_be_pascal_case.symbols = public_symbols
dotnet_naming_rule.public_members_must_be_pascal_case.style = pascal_case_style
dotnet_naming_rule.public_members_must_be_pascal_case.severity = warning

dotnet_naming_symbols.public_symbols.applicable_kinds = class, struct, enum, property, method, event, delegate, namespace
dotnet_naming_symbols.public_symbols.applicable_accessibilities = public, internal, protected, protected_internal

dotnet_naming_style.pascal_case_style.capitalization = pascal_case

# Locals and parameters must be camelCase
dotnet_naming_rule.locals_must_be_camel_case.symbols = local_symbols
dotnet_naming_rule.locals_must_be_camel_case.style = camel_case_style
dotnet_naming_rule.locals_must_be_camel_case.severity = warning

dotnet_naming_symbols.local_symbols.applicable_kinds = parameter, local
dotnet_naming_symbols.local_symbols.applicable_accessibilities = *

dotnet_naming_style.camel_case_style.capitalization = camel_case
```

- [ ] **Step 7: Commit foundation files**

```bash
git add .gitignore .gitattributes .editorconfig docs/ibkr_conduit_design.md docs/superpowers/
git commit -m "chore: init repo with gitignore, gitattributes, editorconfig, and design docs

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Solution and Project Structure

**Files:**
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `IbkrConduit.slnx`
- Create: `src/IbkrConduit/IbkrConduit.csproj`
- Create: `src/IbkrConduit/IbkrConduit.cs`
- Create: `tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj`
- Create: `tests/IbkrConduit.Tests.Unit/SmokeTests.cs`
- Create: `tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj`
- Create: `tests/IbkrConduit.Tests.Integration/SmokeTests.cs`

- [ ] **Step 1: Create `Directory.Build.props`**

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

- [ ] **Step 2: Create `Directory.Packages.props`**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Testing -->
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="coverlet.collector" Version="8.0.1" />
    <!-- Integration testing -->
    <PackageVersion Include="WireMock.Net" Version="2.0.0" />
  </ItemGroup>
</Project>
```

Note: Look up the latest package versions at implementation time — the versions above are current as of 2026-03-26 but may have updates.

- [ ] **Step 3: Create the solution and projects using dotnet CLI**

```bash
cd D:/code/ibkr-conduit

# Create solution (dotnet 10 defaults to .slnx)
dotnet new sln --name IbkrConduit

# Create main library project
mkdir -p src/IbkrConduit
dotnet new classlib --name IbkrConduit --output src/IbkrConduit --framework net10.0

# Create test projects
mkdir -p tests/IbkrConduit.Tests.Unit
dotnet new xunit --name IbkrConduit.Tests.Unit --output tests/IbkrConduit.Tests.Unit --framework net10.0

mkdir -p tests/IbkrConduit.Tests.Integration
dotnet new xunit --name IbkrConduit.Tests.Integration --output tests/IbkrConduit.Tests.Integration --framework net10.0

# Add projects to solution
dotnet sln add src/IbkrConduit/IbkrConduit.csproj
dotnet sln add tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj
dotnet sln add tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj

# Add project references from test projects to library
dotnet add tests/IbkrConduit.Tests.Unit reference src/IbkrConduit/IbkrConduit.csproj
dotnet add tests/IbkrConduit.Tests.Integration reference src/IbkrConduit/IbkrConduit.csproj
```

- [ ] **Step 4: Edit `src/IbkrConduit/IbkrConduit.csproj`**

Replace the generated csproj content with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <PackageId>IbkrConduit</PackageId>
    <Version>0.1.0</Version>
    <Authors>Robert Craig Quillen</Authors>
    <Description>A C#/.NET client library for the Interactive Brokers Client Portal Web API with OAuth 1.0a authentication, multi-tenant session management, rate limiting, and Flex Web Service integration. Not affiliated with Interactive Brokers LLC. Financial trading involves substantial risk of loss. This library is provided as infrastructure software only and is not responsible for trading decisions or financial outcomes.</Description>
    <PackageTags>ibkr;interactive-brokers;trading;oauth;client-portal-api;algorithmic-trading</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageProjectUrl>https://github.com/cquillen/ibkr-conduit</PackageProjectUrl>
    <RepositoryUrl>https://github.com/cquillen/ibkr-conduit</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <None Include="../../README.md" Pack="true" PackagePath="" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Replace stub source file**

Delete the generated `src/IbkrConduit/Class1.cs` and create `src/IbkrConduit/IbkrConduit.cs`:

```csharp
namespace IbkrConduit;

/// <summary>
/// IbkrConduit — a C#/.NET client library for the Interactive Brokers Client Portal Web API.
/// <para>
/// This library is provided as infrastructure software only. It does not provide investment advice
/// and is not responsible for trading decisions or financial outcomes. Not affiliated with
/// Interactive Brokers LLC. Use at your own risk. Always test with a paper trading account first.
/// </para>
/// </summary>
public static class IbkrConduitInfo
{
    /// <summary>
    /// The current version of the IbkrConduit library.
    /// </summary>
    public const string Version = "0.1.0";
}
```

- [ ] **Step 6: Edit test project csproj files**

Edit `tests/IbkrConduit.Tests.Unit/IbkrConduit.Tests.Unit.csproj` — keep the project reference and xunit references generated by the template, but ensure it also references Shouldly and coverlet. Remove any `Version` attributes from `PackageReference` elements (central package management handles versions). Ensure `TargetFramework` is `net10.0`.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/IbkrConduit/IbkrConduit.csproj" />
  </ItemGroup>

</Project>
```

Edit `tests/IbkrConduit.Tests.Integration/IbkrConduit.Tests.Integration.csproj` — same as above plus WireMock.Net:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="WireMock.Net" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/IbkrConduit/IbkrConduit.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 7: Replace generated test files with smoke tests**

Delete generated test files and create `tests/IbkrConduit.Tests.Unit/SmokeTests.cs`:

```csharp
namespace IbkrConduit.Tests.Unit;

using Shouldly;

public class SmokeTests
{
    [Fact]
    public void LibraryVersion_ShouldBeSet()
    {
        IbkrConduitInfo.Version.ShouldNotBeNullOrWhiteSpace();
    }
}
```

Create `tests/IbkrConduit.Tests.Integration/SmokeTests.cs`:

```csharp
namespace IbkrConduit.Tests.Integration;

using Shouldly;

public class SmokeTests
{
    [Fact]
    public void LibraryVersion_ShouldBe_0_1_0()
    {
        IbkrConduitInfo.Version.ShouldBe("0.1.0");
    }
}
```

- [ ] **Step 8: Restore, build, test, and lint**

```bash
cd D:/code/ibkr-conduit
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```

Expected: All four commands succeed with zero warnings, zero failures.

If `dotnet format` reports violations, fix them before proceeding. The `.editorconfig` rules must be satisfied before commit.

- [ ] **Step 9: Commit**

```bash
git add Directory.Build.props Directory.Packages.props IbkrConduit.slnx src/ tests/
git commit -m "chore: add solution with library and test projects

Dual-target net8.0/net10.0 library with xUnit v3 + Shouldly test projects.
Central package management via Directory.Packages.props.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: CI/CD Pipelines

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/publish.yml`
- Create: `.github/workflows/changelog.yml`

- [ ] **Step 1: Create `.github/workflows/ci.yml`**

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET SDKs
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.x
            10.x

      - name: Restore dependencies
        run: dotnet restore

      - name: Check formatting
        run: dotnet format --verify-no-changes

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage"

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          fail_ci_if_error: false
```

- [ ] **Step 2: Create `.github/workflows/publish.yml`**

```yaml
name: Publish to NuGet

on:
  push:
    tags: ['v*.*.*']

jobs:
  publish:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.x

      - name: Extract version from tag
        id: version
        run: echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT

      - name: Pack
        run: dotnet pack src/IbkrConduit --configuration Release -p:PackageVersion=${{ steps.version.outputs.VERSION }} --output ./artifacts

      - name: Push to NuGet
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

- [ ] **Step 3: Create `.github/workflows/changelog.yml`**

```yaml
name: Release Notes

on:
  push:
    tags: ['v*.*.*']

permissions:
  contents: write

jobs:
  release-notes:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Generate release notes
        uses: orhun/git-cliff-action@v4
        id: changelog
        with:
          config: cliff.toml
          args: --latest --strip header
        env:
          OUTPUT: CHANGELOG.md

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          body: ${{ steps.changelog.outputs.content }}
```

Note: `git-cliff` uses conventional commits to generate changelogs. We need a `cliff.toml` config file — create it in the next step.

- [ ] **Step 4: Create `cliff.toml` (git-cliff config)**

```toml
[changelog]
header = """
# Changelog\n
All notable changes to this project will be documented in this file.\n
"""
body = """
{% if version %}\
    ## [{{ version | trim_start_matches(pat="v") }}] - {{ timestamp | date(format="%Y-%m-%d") }}
{% else %}\
    ## [Unreleased]
{% endif %}\
{% for group, commits in commits | group_by(attribute="group") %}
    ### {{ group | striptags | trim | upper_first }}
    {% for commit in commits %}
        - {{ commit.message | upper_first }}\
    {% endfor %}
{% endfor %}\n
"""
trim = true

[git]
conventional_commits = true
filter_unconventional = true
commit_parsers = [
    { message = "^feat", group = "Features" },
    { message = "^fix", group = "Bug Fixes" },
    { message = "^doc", group = "Documentation" },
    { message = "^perf", group = "Performance" },
    { message = "^refactor", group = "Refactoring" },
    { message = "^test", group = "Testing" },
    { message = "^ci", group = "CI/CD" },
    { message = "^chore", group = "Miscellaneous" },
]
filter_commits = false
```

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/ cliff.toml
git commit -m "ci: add CI, NuGet publish, and changelog pipelines

CI runs build/lint/test on PRs. Publish triggers on version tags.
Release notes auto-generated from conventional commits via git-cliff.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Open Source Documents

**Files:**
- Create: `LICENSE`
- Create: `README.md`
- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`
- Create: `CHANGELOG.md`
- Create: `CODE_OF_CONDUCT.md`

- [ ] **Step 1: Create `LICENSE`**

Standard MIT license text with:

```
MIT License

Copyright (c) 2026 Robert Craig Quillen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 2: Create `README.md`**

Structure:

1. `# IbkrConduit` heading
2. Badge row: `[![CI](https://github.com/cquillen/ibkr-conduit/actions/workflows/ci.yml/badge.svg)](...)` `[![NuGet](https://img.shields.io/nuget/v/IbkrConduit)](...)` `[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](...)`
3. One-paragraph description: C#/.NET client library for IBKR CP Web API with OAuth 1.0a, multi-tenant, rate limiting, Flex Web Service
4. **Disclaimer** section — bold header, full financial disclaimer from design doc section 0: not affiliated with Interactive Brokers LLC, infrastructure software only, not investment advice, not responsible for trading decisions or financial outcomes, substantial risk of loss, test with paper account first
5. **Features** section — bulleted list from design doc section 1.1 goals
6. **Quick Start** section — `dotnet add package IbkrConduit`, placeholder showing DI registration pattern (commented out, "coming soon")
7. **IbkrConduit vs IBKR.Sdk.Client** section — comparison table from design doc section 2
8. **Documentation** — link to `docs/ibkr_conduit_design.md`
9. **Contributing** — link to `CONTRIBUTING.md`
10. **Security** — link to `SECURITY.md`
11. **License** — MIT, link to `LICENSE`

- [ ] **Step 3: Create `CONTRIBUTING.md`**

Content:

```markdown
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
```

- [ ] **Step 4: Create `SECURITY.md`**

```markdown
# Security Policy

## Reporting a Vulnerability

IbkrConduit handles financial API credentials (OAuth tokens, RSA private keys, Flex tokens). We take security seriously.

### Preferred: GitHub Private Vulnerability Reporting

Please use [GitHub's private vulnerability reporting](https://github.com/cquillen/ibkr-conduit/security/advisories/new) to report security issues. This ensures the report is visible only to maintainers.

### Fallback: Email

If you cannot use GitHub's reporting, email [craig@thequillens.com](mailto:craig@thequillens.com) with details.

## Response Timeline

- **Acknowledge:** Within 48 hours
- **Assessment:** Within 7 days
- **Resolution target:** Within 30 days for confirmed vulnerabilities

## Scope

**In scope:**
- Credential handling vulnerabilities (token leakage, key material exposure)
- Authentication/signing implementation flaws
- Session management issues
- Log sanitization failures (credentials appearing in logs)
- Dependency vulnerabilities that affect IbkrConduit's security

**Out of scope:**
- Interactive Brokers' own API security — report to [IBKR directly](https://www.interactivebrokers.com/en/index.php?f=ibgStrength&p=report)
- Vulnerabilities in consuming applications
- Trading logic or financial risk concerns

## Disclosure

We follow coordinated disclosure. Please do not file security issues as public GitHub issues.
```

- [ ] **Step 5: Create `CHANGELOG.md`**

```markdown
# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Miscellaneous

- Initial repository scaffolding
```

- [ ] **Step 6: Create `CODE_OF_CONDUCT.md`**

Use the full text of Contributor Covenant v2.1 from https://www.contributor-covenant.org/version/2/1/code_of_conduct/. Set the contact method to `craig@thequillens.com`.

- [ ] **Step 7: Commit**

```bash
git add LICENSE README.md CONTRIBUTING.md SECURITY.md CHANGELOG.md CODE_OF_CONDUCT.md
git commit -m "docs: add open source governance documents

MIT license, README with financial disclaimer, CONTRIBUTING guide,
SECURITY policy, CHANGELOG stub, and Contributor Covenant CoC.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: GitHub Templates and CODEOWNERS

**Files:**
- Create: `.github/pull_request_template.md`
- Create: `.github/ISSUE_TEMPLATE/bug_report.md`
- Create: `.github/ISSUE_TEMPLATE/feature_request.md`
- Create: `.github/ISSUE_TEMPLATE/config.yml`
- Create: `.github/CODEOWNERS`

- [ ] **Step 1: Create `.github/pull_request_template.md`**

```markdown
## Description

<!-- What does this PR do? Why is it needed? -->

## Type of Change

- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update
- [ ] Dependency update
- [ ] CI/CD change

## Testing

- [ ] Unit tests added/updated
- [ ] Integration tests added/updated (WireMock cassettes updated if needed)
- [ ] Tested against paper account (if applicable)

## Checklist

- [ ] Code follows project style conventions (`.editorconfig` enforced)
- [ ] XML documentation comments added/updated for public APIs
- [ ] CHANGELOG.md updated (if user-facing change)
- [ ] No secrets or credentials in code or test fixtures
- [ ] CI pipeline passes (build, lint, test)
```

- [ ] **Step 2: Create `.github/ISSUE_TEMPLATE/bug_report.md`**

```markdown
---
name: Bug Report
about: Report a bug in IbkrConduit
title: "[Bug] "
labels: bug
assignees: ''
---

## Environment

- **IbkrConduit version:**
- **.NET version:**
- **OS:**

## Description

<!-- Clear description of the bug -->

## Steps to Reproduce

1.
2.
3.

## Expected Behavior

<!-- What should happen -->

## Actual Behavior

<!-- What actually happens -->

## Logs / Stack Trace

<!-- If applicable. IMPORTANT: Remove any credentials, tokens, or account identifiers before posting. -->

```
(paste here)
```
```

- [ ] **Step 3: Create `.github/ISSUE_TEMPLATE/feature_request.md`**

```markdown
---
name: Feature Request
about: Suggest a new feature for IbkrConduit
title: "[Feature] "
labels: enhancement
assignees: ''
---

## Problem Statement

<!-- What problem does this solve? -->

## Proposed Solution

<!-- How should it work? -->

## Alternatives Considered

<!-- Any other approaches you considered? -->

## IBKR API Reference

<!-- Link to relevant IBKR API documentation if applicable -->
```

- [ ] **Step 4: Create `.github/ISSUE_TEMPLATE/config.yml`**

```yaml
blank_issues_enabled: false
contact_links:
  - name: Security Vulnerability
    url: https://github.com/cquillen/ibkr-conduit/security/advisories/new
    about: Please report security vulnerabilities through GitHub's private vulnerability reporting. Do not open a public issue.
```

- [ ] **Step 5: Create `.github/CODEOWNERS`**

```
* @cquillen
```

- [ ] **Step 6: Commit**

```bash
git add .github/
git commit -m "chore: add GitHub templates and CODEOWNERS

PR template, bug report/feature request issue templates,
blank issues disabled with security reporting link.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 6: Claude Code Configuration

**Files:**
- Create: `CLAUDE.md`
- Create: `.claude/settings.json`
- Create: `.claude/rules/code-style.md`
- Create: `.claude/rules/build-quality.md`
- Create: `.claude/rules/git-conventions.md`
- Create: `.claude/rules/architecture.md`
- Create: `.claude/rules/testing.md`
- Create: `.claude/rules/security.md`

- [ ] **Step 1: Create `CLAUDE.md`**

```markdown
# IbkrConduit

C#/.NET client library for the Interactive Brokers Client Portal Web API with OAuth 1.0a authentication.

## Key References

- **Design document:** `docs/ibkr_conduit_design.md` — authoritative design reference for all implementation decisions
- **Implementation status:** `docs/implementation-status.md` — check at session start to know what's done and what's next
- **Specs and plans:** `docs/superpowers/specs/` and `docs/superpowers/plans/`

## Commands

- **Build:** `dotnet build --configuration Release`
- **Test:** `dotnet test --configuration Release`
- **Lint:** `dotnet format --verify-no-changes`
- **Full check:** `dotnet build --configuration Release && dotnet test --configuration Release && dotnet format --verify-no-changes`

## Rules

See `.claude/rules/` for detailed guidance on code style, build quality, git conventions, architecture, testing, and security.
```

- [ ] **Step 2: Create `.claude/settings.json`**

```json
{
  "permissions": {
    "allow": [
      "Bash(dotnet *)",
      "Bash(git *)",
      "Bash(gh *)",
      "Bash(mkdir *)",
      "Bash(mv *)",
      "Bash(cp *)",
      "Bash(rm *)",
      "Bash(ls *)",
      "Bash(cat *)",
      "Bash(head *)",
      "Bash(tail *)",
      "Bash(find *)",
      "Bash(grep *)",
      "Bash(pwd)",
      "Bash(which *)"
    ]
  }
}
```

- [ ] **Step 3: Create `.claude/rules/code-style.md`**

```markdown
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
```

- [ ] **Step 4: Create `.claude/rules/build-quality.md`**

```markdown
---
description: Build quality rules — zero warnings, central package management
globs: "**/*.csproj,**/Directory.Build.props,**/Directory.Packages.props"
---

- Zero warnings policy — `TreatWarningsAsErrors` is enabled in `Directory.Build.props`
- Do not add `<NoWarn>` to suppress warnings without an explicit comment explaining why it is necessary
- All NuGet package versions MUST be defined in `Directory.Packages.props` at the repo root
- Individual `.csproj` files use `<PackageReference Include="..." />` without `Version` attributes
- Never add a `Version` attribute to a `PackageReference` in a `.csproj` file
```

- [ ] **Step 5: Create `.claude/rules/git-conventions.md`**

```markdown
---
description: Git workflow and commit message conventions
---

- Use Conventional Commits format for all commit messages:
  - `feat:` — new feature
  - `fix:` — bug fix
  - `docs:` — documentation only
  - `chore:` — maintenance, dependencies, tooling
  - `refactor:` — code change that neither fixes nor adds
  - `test:` — adding or updating tests
  - `ci:` — CI/CD pipeline changes
- Follow GitHub Flow: feature branches off `main`, merge via pull request
- CI must pass before merge — build, lint, and tests are required status checks
- Never commit secrets, `.pem` files, private keys, tokens, or credentials
- Never commit `bin/`, `obj/`, or build artifacts
```

- [ ] **Step 6: Create `.claude/rules/architecture.md`**

```markdown
---
description: Architectural principles for IbkrConduit implementation
globs: "src/**"
---

- Multi-tenant by design — no global or static mutable state. Each tenant has independent session, rate limiters, and credentials.
- Storage-agnostic — credentials are accepted as pre-loaded objects (`RSA`, strings), not file paths or cloud-specific constructs
- All cryptographic operations use `System.Security.Cryptography` only — no external crypto libraries
- Refer to `docs/ibkr_conduit_design.md` for detailed design decisions, API behaviors, and implementation guidance
- The library handles IBKR API quirks (session lifecycle, question/reply flow, rate limits) so consumers don't have to
```

- [ ] **Step 7: Create `.claude/rules/testing.md`**

```markdown
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
```

- [ ] **Step 8: Create `.claude/rules/security.md`**

```markdown
---
description: Security rules for handling financial API credentials
---

- Never include credentials, tokens, private keys, or key material in code or test fixtures
- Never commit `.pem`, `.pfx`, or `.key` files — the `.gitignore` blocks these but remain vigilant
- Sanitize all log output — ensure no credential material appears in logs
- The financial disclaimer must appear in README.md and the NuGet package description
- Test fixtures must use synthetic/fake credential values that are clearly not real
```

- [ ] **Step 9: Commit**

```bash
git add CLAUDE.md .claude/
git commit -m "chore: add Claude Code configuration and rules

CLAUDE.md entry point, .claude/settings.json permissions,
and scoped rules for code style, build quality, git, architecture,
testing, and security.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 7: Implementation Status Tracker

**Files:**
- Create: `docs/implementation-status.md`

- [ ] **Step 1: Create `docs/implementation-status.md`**

```markdown
# IbkrConduit — Implementation Status

Tracks implementation progress against [ibkr_conduit_design.md](ibkr_conduit_design.md).
Updated at the end of each implementation session.

## Status Key

| Status | Meaning |
|---|---|
| Not Started | No work begun |
| Spec'd | Design spec written in `docs/superpowers/specs/` |
| In Progress | Implementation underway |
| Done | Implemented and tested |

---

## Repo Scaffolding

| Capability | Status | Spec |
|---|---|---|
| Git init and foundation files | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Solution and project structure | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| CI/CD pipelines | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Open source documents | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| GitHub templates and config | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Claude Code configuration | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Implementation status tracker | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| GitHub repo creation and push | Not Started | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Branch protection setup | Not Started | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |

---

## Phase 1 — Core Authentication and Session

| Capability | Status | Spec |
|---|---|---|
| OAuth 1.0a crypto (RSA-OAEP, DH, HMAC-SHA256) | Not Started | — |
| Live session token acquisition and refresh | Not Started | — |
| Multi-tenant session manager | Not Started | — |
| Tickle timer (PeriodicTimer per tenant) | Not Started | — |
| Brokerage session initialization | Not Started | — |
| Question suppression | Not Started | — |
| Token bucket rate limiters (global + per-endpoint) | Not Started | — |
| OAuthSigningHandler DelegatingHandler | Not Started | — |
| Polly resilience pipeline | Not Started | — |
| 429 adaptive response logic | Not Started | — |
| Maintenance window detection and handling | Not Started | — |
| Unit tests — OAuth crypto | Not Started | — |
| Unit tests — session manager concurrency | Not Started | — |
| Unit tests — rate limiter behavior | Not Started | — |
| WireMock integration tests — session lifecycle | Not Started | — |

---

## Phase 2 — Order and Portfolio APIs

| Capability | Status | Spec |
|---|---|---|
| Order submission with question/reply flow | Not Started | — |
| Order cancellation | Not Started | — |
| Conid resolution and caching | Not Started | — |
| Position and account data retrieval | Not Started | — |
| Current session orders and fills endpoints | Not Started | — |
| Market data snapshot with pre-flight handling | Not Started | — |
| Historical market data | Not Started | — |
| WireMock edge case scenarios — order flow | Not Started | — |

---

## Phase 3 — WebSocket and Flex

| Capability | Status | Spec |
|---|---|---|
| WebSocket client with heartbeat and reconnection | Not Started | — |
| sor topic (order updates and history) | Not Started | — |
| smd topic (market data streaming) | Not Started | — |
| spl topic (P&L streaming) | Not Started | — |
| Flex Web Service client (two-step async) | Not Started | — |
| Flex XML response parsing | Not Started | — |
| Per-tenant Flex configuration model | Not Started | — |
| WireMock scenarios — WebSocket and Flex | Not Started | — |

---

## Phase 4 — Open Source Hardening

| Capability | Status | Spec |
|---|---|---|
| NuGet packaging and metadata | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| GitHub Actions CI/CD | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Branch protection | Not Started | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| PR and issue templates | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| CONTRIBUTING.md | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| SECURITY.md | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| README with quick start | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
| Samples project | Not Started | — |
| API documentation (XML comments on all public types) | Not Started | — |
| CHANGELOG.md | In Progress | [scaffolding](superpowers/specs/2026-03-26-repo-scaffolding-design.md) |
```

- [ ] **Step 2: Commit**

```bash
git add docs/implementation-status.md
git commit -m "docs: add implementation status tracker

Tracks progress across all design doc phases.
Check at session start to know what's done and what's next.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

### Task 8: Create GitHub Repo and Push

- [ ] **Step 1: Create GitHub repository**

```bash
gh repo create cquillen/ibkr-conduit --public --description "C#/.NET client library for the Interactive Brokers Client Portal Web API with OAuth 1.0a authentication" --source D:/code/ibkr-conduit --push
```

If `gh` is not authenticated or not available, prompt the user to:
1. Install GitHub CLI: https://cli.github.com/
2. Authenticate: `gh auth login`
3. Then re-run the command

- [ ] **Step 2: Verify remote and push**

```bash
git remote -v
git push -u origin main
```

Expected: All commits pushed to `cquillen/ibkr-conduit` on GitHub.

- [ ] **Step 3: Verify CI runs on GitHub**

```bash
gh run list --limit 1
```

Expected: CI workflow triggered by the push to `main`. Wait for it to complete and verify it passes.

```bash
gh run watch
```

If CI fails, diagnose and fix before proceeding.

---

### Task 9: Branch Protection and Repo Settings

- [ ] **Step 1: Enable private vulnerability reporting**

```bash
gh api repos/cquillen/ibkr-conduit/private-vulnerability-reporting --method PUT
```

- [ ] **Step 2: Configure branch protection on main**

```bash
gh api repos/cquillen/ibkr-conduit/branches/main/protection --method PUT --input - <<'EOF'
{
  "required_status_checks": {
    "strict": true,
    "contexts": ["build-and-test"]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": true,
    "required_approving_review_count": 1
  },
  "restrictions": null
}
EOF
```

- [ ] **Step 3: Verify branch protection**

```bash
gh api repos/cquillen/ibkr-conduit/branches/main/protection
```

Expected: Response shows required status checks, required reviews, and admin enforcement.

---

### Task 10: Update Implementation Tracker and Final Verification

- [ ] **Step 1: Update `docs/implementation-status.md`**

Change all "In Progress" items in the Repo Scaffolding section and Phase 4 section to "Done". Change "GitHub repo creation and push" and "Branch protection setup" to "Done".

- [ ] **Step 2: Final local verification**

```bash
cd D:/code/ibkr-conduit
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
```

Expected: All pass with zero warnings, zero failures.

- [ ] **Step 3: Commit and push tracker update**

Since branch protection is now enabled, this needs to go through a PR:

```bash
git checkout -b chore/update-implementation-status
git add docs/implementation-status.md
git commit -m "docs: mark scaffolding tasks as done in implementation tracker

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
git push -u origin chore/update-implementation-status
gh pr create --title "docs: mark scaffolding tasks as done" --body "Updates implementation-status.md to reflect completed scaffolding work."
```

Then merge the PR after CI passes (may need to self-approve or temporarily adjust branch protection for the first PR).
