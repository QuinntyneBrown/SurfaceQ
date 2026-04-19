# 49 — Performance benchmark: 50-file cold run

**Traces to:** L2-015

## Goal
A 50-file synthetic fixture completes `generate` in under 5 seconds on a modern developer laptop (cold start).

## Failing test
Benchmark test in `test/SurfaceQ.Integration.Tests/Performance` generates a fixture with 50 typical files, spawns the CLI as a fresh process (cold), times wall-clock duration. Assert < 5s. Skip on slow CI (attribute-gated); run on a designated perf job.

## Implementation
No code change expected first time; optimize only if test fails.

## Done when
- Test green on the perf job.
