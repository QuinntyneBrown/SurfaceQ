# 05 — Missing manifest → exit 2

**Traces to:** L2-004

## Goal
Running `surfaceq generate` in a directory with no `ng-package.json` (no ancestor either) exits `2` and prints an error to stderr including the search path.

## Failing test
In `test/SurfaceQ.Cli.Tests/GenerateMissingManifestTests.cs`, invoke the CLI pointing `--project` at an empty temp directory. Assert exit code `2` and stderr contains the path.

## Implementation
If locator returns `null`, write error to stderr (via logger) and return `2`.

## Done when
- Test green.
