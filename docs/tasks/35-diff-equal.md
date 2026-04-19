# 35 — `diff` equal → exit 0, no output

**Traces to:** L2-003

## Goal
When files match, `diff` exits `0` and prints nothing to stdout.

## Failing test
After a successful `generate`, run `diff`. Assert exit `0` and stdout is empty.

## Implementation
Command handler: compare; if equal, return `0` silently.

## Done when
- Test green.
