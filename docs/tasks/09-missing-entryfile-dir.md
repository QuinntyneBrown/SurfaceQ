# 09 — Missing `entryFile` directory → exit 2

**Traces to:** L2-005

## Goal
If the directory containing the resolved `entryFile` does not exist, the CLI exits `2`.

## Failing test
Manifest points at `nonexistent/public-api.ts`. Run `surfaceq generate`. Assert exit `2` and stderr identifies the missing directory.

## Implementation
After resolving `ScanRoot`, `Directory.Exists` check; if false, raise `ManifestException`.

## Done when
- Test green.
