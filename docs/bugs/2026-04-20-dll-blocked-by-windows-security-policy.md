# Bug: `.dll` files blocked by Windows Application Control policy

**Date:** 2026-04-20
**Severity:** Medium — affects testing locally installed global tool on Windows.

## Repro

1. Build and install tool locally: `dotnet tool install --global --add-source src/SurfaceQ.Cli/bin/Release SurfaceQ`
2. Run: `surfaceq generate --project <lib>`

## Actual

```
System.IO.FileLoadException: Could not load file or assembly
'C:\Users\...\SurfaceQ.Core.dll'. An Application Control policy
has blocked this file. (0x800711C7)
```

## Workaround

NuGet-published packages avoid this Windows security check when downloaded and extracted by dotnet tool installer. Local build installations may trigger this policy.

Can be bypassed with: `Unblock-File -Path <dll>` (PowerShell) or file properties dialog.

## Notes

Not blocking NuGet publishing tests; use published package from NuGet for integration tests instead of local builds.
