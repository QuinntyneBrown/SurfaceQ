# 31 — `generate` read-only file → exit 2

**Traces to:** L2-019

## Goal
If `public-api.ts` exists and is read-only, `generate` exits `2` with a permissions error.

## Failing test
Create `public-api.ts` and set `File.SetAttributes(..., FileAttributes.ReadOnly)`. Run `generate`. Assert exit `2`, stderr mentions permission / read-only, and the file contents are unchanged.

## Implementation
Catch `UnauthorizedAccessException` and `IOException` around `File.WriteAllText`; log and return `2`.

## Done when
- Test green on all three OSes in CI.
