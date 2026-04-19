# 32 — `check` matches → exit 0

**Traces to:** L2-002

## Goal
If the on-disk `public-api.ts` matches generated output exactly, `check` exits `0` and does not write.

## Failing test
Run `generate`, record file mtime. Then run `check`. Assert exit `0` and mtime unchanged.

## Implementation
Command handler: render expected, read actual, `StringComparer.Ordinal.Equals`. No I/O write.

## Done when
- Test green.
