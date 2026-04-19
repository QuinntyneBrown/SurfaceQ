# 38 — Logging: stderr vs stdout routing

**Traces to:** L2-016

## Goal
Warnings and errors go to stderr. Informational and verbose output go to stdout.

## Failing test
Run `generate` against a fixture that produces one info log (fallback) and one warning (default-export skipped). Capture stdout and stderr separately. Assert info line appears in stdout only, warning line appears in stderr only.

## Implementation
Configure `Microsoft.Extensions.Logging.Console` with two loggers/providers or one provider routing by level. Simplest path: a small custom `ILoggerProvider` that writes `>= Warning` to `Console.Error`.

## Done when
- Test green.
