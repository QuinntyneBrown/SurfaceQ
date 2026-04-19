# 01 — Solution skeleton + `--version`

**Traces to:** L2-021

## Goal
A `surfaceq --version` command prints the SurfaceQ semantic version and exits `0`.

## Failing test (write first)
In `test/SurfaceQ.Cli.Tests/VersionTests.cs`:
```csharp
// Acceptance Test
// Traces to: L2-021
// Description: `surfaceq --version` prints a semver and exits 0.
```
Run the CLI entry point with `new[] { "--version" }`, capture stdout, assert it matches `^\d+\.\d+\.\d+`, and assert exit code is `0`.

## Implementation (keep tiny)
1. Create solution `SurfaceQ.sln`.
2. Add projects: `src/SurfaceQ.Cli` (console app), `test/SurfaceQ.Cli.Tests` (xUnit).
3. In `Program.cs`, wire `System.CommandLine` with a root command and a `--version` option that writes `typeof(Program).Assembly.GetName().Version` and returns `0`.

## Done when
- Test is green.
- `dotnet run --project src/SurfaceQ.Cli -- --version` prints a version locally.
