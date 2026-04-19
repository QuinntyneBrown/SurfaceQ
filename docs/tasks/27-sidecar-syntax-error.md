# 27 — Sidecar: syntax error → exit 2

**Traces to:** L2-018

## Goal
A TS file with a syntax error causes `generate` to exit `2` and stderr identifies the file and line.

## Failing test
Fixture file containing `export class {` (incomplete). Run `generate`. Assert exit `2` and stderr mentions the relative file path and a line number.

## Implementation
Sidecar: check `sourceFile.parseDiagnostics`; when non-empty, return an `error` object with `file` and `line`. CLI: log and return `2`.

## Done when
- Test green.
