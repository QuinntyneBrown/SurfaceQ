# Bug: packed `node_modules/typescript` has duplicated `lib/` segment, breaking `require('typescript')`

**Date:** 2026-04-20
**Severity:** High — compounds with sidecar.js resolution bug to make the CLI non-functional when installed as a global tool.

## Repro

1. `dotnet pack src/SurfaceQ.Cli/SurfaceQ.Cli.csproj -c Release`.
2. Install the resulting `SurfaceQ.0.1.0.nupkg` as a global tool.
3. Run `surfaceq generate --project <angular-lib>`.

## Expected

Sidecar starts, processes input.

## Actual

Sidecar dies immediately on `require('typescript')`:

```
Error: Cannot find module
'.../content/sidecar/node_modules/typescript/lib/typescript.js'.
Please verify that the package.json has a valid "main" entry
```

Surfaced to the caller as: `sidecar closed stdout without responding` (see `SidecarClient.cs:34`).

## Root cause

In `src/SurfaceQ.Cli/SurfaceQ.Cli.csproj`:

```xml
<PropertyGroup>
  <SidecarNodeDir>$(MSBuildThisFileDirectory)..\SurfaceQ.Sidecar.Node</SidecarNodeDir>
</PropertyGroup>
...
<None Include="$(SidecarNodeDir)\node_modules\typescript\**\*">
  <Pack>true</Pack>
  <PackagePath>tools/net8.0/any/content/sidecar/node_modules/typescript/%(RecursiveDir)</PackagePath>
</None>
```

Because `SidecarNodeDir` contains an un-normalized `..\` segment, MSBuild evaluates `%(RecursiveDir)` for matched files with an extra leading segment. The on-disk file:

```
src/SurfaceQ.Sidecar.Node/node_modules/typescript/lib/typescript.js
```

gets packed as:

```
tools/net8.0/any/content/sidecar/node_modules/typescript/lib/lib/typescript.js
                                                           ^^^^^^^^^ duplicated
```

`require('typescript')` resolves to `<pkg>/lib/typescript.js` per its `package.json "main"`, which no longer exists at that path, so Node throws MODULE_NOT_FOUND.

Confirmed by inspecting the `.nupkg`:

```
tools/net8.0/any/content/sidecar/node_modules/typescript/lib/lib/typescript.js
tools/net8.0/any/content/sidecar/node_modules/typescript/lib/lib/tsc.js
tools/net8.0/any/content/sidecar/node_modules/typescript/lib/cs/lib/cs/diagnosticMessages.generated.json
```

Every subpath segment under the matched root is duplicated.

## Fix

NuGet already auto-appends `%(RecursiveDir)%(Filename)%(Extension)` to any `PackagePath` that ends with `/`. Writing `%(RecursiveDir)` ourselves caused it to be expanded twice (once in the literal `PackagePath`, once by NuGet's auto-append), producing e.g. `lib/lib/typescript.js`.

Drop the explicit `%(RecursiveDir)`:

```xml
<PackagePath>tools/net8.0/any/content/sidecar/node_modules/typescript/</PackagePath>
```
