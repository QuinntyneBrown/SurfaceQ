# 20 — Sidecar: spawn + JSON round-trip

**Traces to:** L2-013

## Goal
The .NET host spawns the Node sidecar and exchanges one JSON-RPC request/response.

## Failing test
In `test/SurfaceQ.Integration.Tests`, send a `ping` request, assert the sidecar echoes the `id` and returns `{ "result": "pong" }`.

## Implementation
- `src/SurfaceQ.Sidecar.Node/sidecar.js`: read lines from stdin, parse JSON, handle `method: "ping"` → respond pong.
- `src/SurfaceQ.Sidecar/SidecarClient.cs`: start `node sidecar.js` via `Process.Start` with explicit args (no shell), write one line, read one line.

## Done when
- Test green. Process is terminated after the test.
