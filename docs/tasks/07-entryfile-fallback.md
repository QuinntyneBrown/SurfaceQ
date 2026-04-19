# 07 — Fallback to `src/public-api.ts`

**Traces to:** L2-005

## Goal
When `ng-package.json` omits `entryFile`, the reader falls back to `src/public-api.ts` and logs an informational message.

## Failing test
Manifest content `{}`. Assert `EntryFile` ends with `src/public-api.ts` and that a captured logger received one `Information` message mentioning the fallback.

## Implementation
If JSON property missing or null, substitute `"src/public-api.ts"` and log info.

## Done when
- Test green. Task 06 still passes.
