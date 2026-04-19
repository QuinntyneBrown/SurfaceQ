# 46 — Bundle Node.js per RID

**Traces to:** L2-014

## Goal
The NuGet package contains Node.js binaries for Windows/Linux/macOS × x64/ARM64, and the CLI selects the correct one at startup.

## Failing test
- Unit test: given `RuntimeInformation.RuntimeIdentifier`, `NodeResolver.ResolveNodePath()` returns a path that exists under the installed tool directory.
- Packaging test: `dotnet pack` and inspect the resulting `.nupkg` (it's a zip); assert all six RID folders are present.

## Implementation
At build time, download/copy Node binaries into `content/node/<rid>/`. `<Content>` MSBuild items include them. Runtime resolver returns `content/node/<rid>/node(.exe)`.

## Done when
- Tests green. Package size documented.
