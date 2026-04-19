# 29 — `generate` overwrites existing file

**Traces to:** L2-001

## Goal
If `public-api.ts` already exists, `generate` overwrites it.

## Failing test
Pre-write `public-api.ts` with arbitrary content `"// stale"`. Run `generate`. Assert file content no longer contains `// stale`.

## Implementation
`File.WriteAllText` already overwrites. Just confirm behavior with the test.

## Done when
- Test green.
