# 06 — Parse `entryFile` field

**Traces to:** L2-005

## Goal
A `ManifestReader.Read(path)` returns a `ProjectContext` whose `EntryFile` equals the manifest's `entryFile` value, resolved against the manifest directory.

## Failing test
Write an `ng-package.json` with `{ "entryFile": "src/public-api.ts" }`. Call reader. Assert `EntryFile == "<tempDir>/src/public-api.ts"` and `ScanRoot == "<tempDir>/src"`.

## Implementation
Plain record `ProjectContext(string ManifestPath, string EntryFile, string ScanRoot)`. Parse with `System.Text.Json` (`JsonDocument`). Read string property `entryFile`.

## Done when
- Test green.
