---
description: Build quality rules — zero warnings, central package management
globs: "**/*.csproj,**/Directory.Build.props,**/Directory.Packages.props"
---

- Zero warnings policy — `TreatWarningsAsErrors` is enabled in `Directory.Build.props`
- Do not add `<NoWarn>` to suppress warnings without an explicit comment explaining why it is necessary
- All NuGet package versions MUST be defined in `Directory.Packages.props` at the repo root
- Individual `.csproj` files use `<PackageReference Include="..." />` without `Version` attributes
- Never add a `Version` attribute to a `PackageReference` in a `.csproj` file
