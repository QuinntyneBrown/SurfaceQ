# 44 — Ignore stray config files

**Traces to:** L2-022

## Goal
Files named `surfaceq.config.json` or `.surfaceqrc` are ignored in v1.

## Failing test
Fixture contains a `surfaceq.config.json` with invalid JSON alongside a valid Angular library. Run `generate`. Assert exit `0` (proving the file was not read).

## Implementation
No code reads those files. Test locks in behavior.

## Done when
- Test green.
