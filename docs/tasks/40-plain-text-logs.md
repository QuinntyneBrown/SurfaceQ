# 40 — Plain-text logs (no ANSI)

**Traces to:** L2-016

## Goal
Log output is plain text — no ANSI escape sequences, no JSON framing.

## Failing test
Capture stdout+stderr from a `generate` run under `--verbosity detailed`. Assert no `\u001b[` escape anywhere; assert each line is not valid JSON (or does not start with `{`).

## Implementation
Configure console logger with `FormatterName = "simple"`, `DisableColors = true`, or set `NO_COLOR` equivalent. Prefer a minimal custom formatter that writes `level: message`.

## Done when
- Test green.
