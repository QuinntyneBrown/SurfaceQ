# 47 — Bundle `typescript` npm package

**Traces to:** L2-014

## Goal
The sidecar ships with `typescript` pre-installed; no `npm install` at runtime.

## Failing test
Packaging test: assert `.nupkg` contains `content/sidecar/node_modules/typescript/package.json` at a pinned version.

## Implementation
In `src/SurfaceQ.Sidecar.Node/`: `package.json` with pinned `typescript` version; run `npm ci` at .NET build time (pre-build target), copy `node_modules` into output.

## Done when
- Test green.
- Confirmed: no `npm` process is spawned in any runtime test (task 43 covers network).
