# 19 — Empty scan root → header-only file

**Traces to:** L2-006

## Goal
When the scan root contains zero eligible `.ts` files, `generate` writes a file containing only the header and exits `0` with an info log.

## Failing test
Fixture with only `index.ts` (excluded). Run `generate`. Assert `public-api.ts` written, contents equal the header block plus a trailing newline, exit code `0`.

## Implementation
Command handler: if walker returns empty list, still call renderer and write file.

## Done when
- Test green.
