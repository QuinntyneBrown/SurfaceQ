# 37 — `diff` never writes

**Traces to:** L2-003

## Goal
`diff` must not modify `public-api.ts` in any scenario.

## Failing test
Snapshot the file's hash + mtime, run `diff` (both equal and differing scenarios), assert hash and mtime unchanged after each run.

## Implementation
Audit: confirm `diff` command handler never touches `File.Write*`.

## Done when
- Test green.
