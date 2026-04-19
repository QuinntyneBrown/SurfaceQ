# 03 — Project locator: current directory

**Traces to:** L2-004

## Goal
A `ProjectLocator.Locate(startPath)` method returns the path of an `ng-package.json` found directly in `startPath`.

## Failing test
In `test/SurfaceQ.Core.Tests/ProjectLocatorTests.cs`:
- Create a temp directory, drop an empty `ng-package.json` in it.
- Call `new ProjectLocator().Locate(tempDir)`.
- Assert returned path equals `Path.Combine(tempDir, "ng-package.json")`.

## Implementation
New class `SurfaceQ.Core.ProjectLocator` with a single method `string Locate(string startPath)`. For this task it only checks the start directory.

## Done when
- Test green.
