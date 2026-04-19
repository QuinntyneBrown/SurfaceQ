# 43 — No network

**Traces to:** L2-017

## Goal
No outbound connection is opened during any `surfaceq` run.

## Failing test
A test wraps a `generate` invocation with a loopback-only firewall using a process-level check: count open sockets (via `System.Net.NetworkInformation.IPGlobalProperties.GetActiveTcpConnections()` filtered by the process id) before and after the run; assert zero non-loopback connections opened by the SurfaceQ process or its sidecar child.

## Implementation
Audit code: no `HttpClient`, `WebClient`, `TcpClient`, or `npm install`. No update checks.

## Done when
- Test green.
