# 08 — Malformed manifest → exit 2

**Traces to:** L2-005

## Goal
A malformed-JSON `ng-package.json` causes the CLI to exit `2` with a clear error naming the file and parse location.

## Failing test
Drop `ng-package.json` containing `{ this is not json`. Run `surfaceq generate`. Assert exit `2` and stderr mentions both the manifest path and the word `JSON`.

## Implementation
Catch `JsonException` in the reader; rethrow as a typed `ManifestException` caught by the command handler which logs and returns `2`.

## Done when
- Test green.
