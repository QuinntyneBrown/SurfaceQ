# Observation: NuGet publish has ~60-120s indexing delay

**Date:** 2026-04-20
**Severity:** Low — expected behavior, not a bug.

## Observation

After successful GitHub Actions publish (confirmed via `gh run list`), the package does not immediately appear when querying:
- `https://api.nuget.org/v3-flatcontainer/surfaceq/index.json`
- `dotnet tool install --global SurfaceQ`

The new version appears on NuGet after ~60-120 seconds.

## Note

This is normal NuGet CDN behavior. For CI/CD loops that install and test the published tool, add a wait between publish and install (120+ seconds is safe).
