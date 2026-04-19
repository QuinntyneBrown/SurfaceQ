# 04 — Project locator: walk upward

**Traces to:** L2-004

## Goal
When `ng-package.json` is not in `startPath`, the locator walks up parent directories until it finds one.

## Failing test
Temp dir structure: `root/ng-package.json` and `root/a/b/c/`. Call `Locate("root/a/b/c")`. Assert return equals `root/ng-package.json`.

## Implementation
Loop: `DirectoryInfo dir = new(startPath); while (dir != null) { check; dir = dir.Parent; }`.

## Done when
- Test green.
- Test 03 still passes.
