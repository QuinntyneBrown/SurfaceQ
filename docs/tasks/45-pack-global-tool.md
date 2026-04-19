# 45 — Package as .NET global tool

**Traces to:** L2-014

## Goal
`SurfaceQ.Cli.csproj` packs as a .NET global tool (`PackAsTool=true`) and installs via `dotnet tool install`.

## Failing test
Shell integration test:
1. `dotnet pack` the CLI into a local NuGet folder.
2. `dotnet tool install --global --add-source <folder> SurfaceQ`.
3. Run `surfaceq --version`.
4. Assert exit `0` and semver printed.

## Implementation
Add `<PackAsTool>true</PackAsTool>`, `<ToolCommandName>surfaceq</ToolCommandName>`, `<PackageId>SurfaceQ</PackageId>`.

## Done when
- Test green.
