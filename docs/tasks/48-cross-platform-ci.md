# 48 — Cross-platform determinism CI matrix

**Traces to:** L2-007, L2-012

## Goal
A GitHub Actions matrix runs the full acceptance suite on Windows, Linux, and macOS, and an artifact-comparison step asserts the generated `public-api.ts` for a canonical fixture hashes identically across all three.

## Failing test (as a CI job)
Each OS job generates `public-api.ts` for the canonical fixture and uploads it as an artifact. A final job downloads all three and asserts SHA-256 equality.

## Implementation
`.github/workflows/ci.yml` with `strategy.matrix.os: [ubuntu-latest, windows-latest, macos-latest]`, plus a `determinism` aggregator job.

## Done when
- CI green on all three OSes and determinism job passes.
