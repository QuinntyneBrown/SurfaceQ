# 33 — `check` drift → exit 1

**Traces to:** L2-002

## Goal
If drift exists, `check` exits `1` and prints a concise message (no full diff).

## Failing test
Tamper with `public-api.ts` after generate (append whitespace). Run `check`. Assert exit `1` and stdout mentions "drift" but does not contain `+` / `-` diff markers.

## Implementation
On inequality, write one line to stdout ("Drift detected in ...") and return `1`.

## Done when
- Test green.
