# 50 — 200-file warm run and 500-file memory ceiling

**Traces to:** L2-015

## Goal
A 200-file fixture completes warm `generate` in under 10 seconds. A 500-file fixture runs with peak working set under 512 MB.

## Failing test
Two tests:
1. Warm timing — run once to warm, then time second run; assert < 10s.
2. Memory — launch CLI, poll `Process.WorkingSet64` every 50 ms until exit, assert peak < 512 MB.

## Implementation
Optimize only if tests fail (e.g., batch file list to sidecar in a single request; avoid per-file process spawns).

## Done when
- Both tests green on the perf job.
