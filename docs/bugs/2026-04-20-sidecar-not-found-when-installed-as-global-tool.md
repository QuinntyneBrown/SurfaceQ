# Bug: `sidecar.js not found` when CLI installed as a global dotnet tool

**Date:** 2026-04-20
**Reporter:** loop run against C:\projects\Clarity\src\Clarity.Web
**Severity:** High — CLI is completely non-functional when installed as a global tool.

## Repro

1. Pack & install globally: `eng\scripts\install-cli.bat`.
2. Against any Angular library: `surfaceq generate --project <ng-package-dir>`.

## Expected

`public-api.ts` is generated.

## Actual

```
Unhandled exception: System.IO.FileNotFoundException: sidecar.js not found;
walked upward from C:\Users\quinn\.dotnet\tools\.store\surfaceq\0.1.0\surfaceq\0.1.0\tools\net8.0\any\
   at SurfaceQ.Cli.OutputPipeline.ResolveSidecarScript() in OutputPipeline.cs:line 157
```

## Root cause

`OutputPipeline.ResolveSidecarScript` (src/SurfaceQ.Cli/OutputPipeline.cs:145) only looks for the dev-time repo layout:

```
<walk-up>/src/SurfaceQ.Sidecar.Node/sidecar.js
```

But the `.csproj` packs the sidecar into the tool package at:

```
tools/net8.0/any/content/sidecar/sidecar.js
```

which lands at `<AppContext.BaseDirectory>/content/sidecar/sidecar.js` once installed. The resolver never checks that location, so it fails for every tool-installed invocation.

## Fix

Check the packaged layout (`<BaseDirectory>/content/sidecar/sidecar.js`) first; fall back to the dev walk-up for running from the repo.
