# 39 — `--verbosity quiet` and `diagnostic`

**Traces to:** L2-016

## Goal
`--verbosity quiet` suppresses stdout on success; `--verbosity diagnostic` emits detailed trace output.

## Failing test
Two tests. Run `generate --verbosity quiet` on a clean fixture: stdout empty. Run `generate --verbosity diagnostic`: stdout contains at least one trace line mentioning "sidecar" or "walker".

## Implementation
Add a global `--verbosity` option; map to `LogLevel` (`quiet`→None, `minimal`→Warning, `normal`→Information, `detailed`→Debug, `diagnostic`→Trace). Set the logger minimum level.

## Done when
- Both tests green.
