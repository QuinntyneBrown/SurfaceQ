# 26 — Sidecar: skip default export with warning

**Traces to:** L2-010

## Goal
`export default ...` produces a `default-export-skipped` warning; the run still exits `0`.

## Failing test
Fixture file with one default export and no named exports. Run `generate`. Assert:
- Output file is written (header only).
- stderr contains `warn:` and the relative file path.
- Exit code `0`.

## Implementation
Sidecar: detect `ExportAssignment` (default) and `export default class/function`; emit a `warnings` entry. CLI: map sidecar warnings to logger `Warning`.

## Done when
- Test green.
