# 34 — `check` error → exit 2

**Traces to:** L2-002

## Goal
Any execution error (missing manifest, syntax error, etc.) causes `check` to exit `2`.

## Failing test
Run `check` in an empty directory with no manifest. Assert exit `2` and stderr message.

## Implementation
Reuse the same error paths as `generate`.

## Done when
- Test green.
