# 10 — File walker: basic `.ts` discovery

**Traces to:** L2-006

## Goal
`SourceFileWalker.Walk(context)` returns every `.ts` file under `ScanRoot` recursively.

## Failing test
Create temp tree with `src/a.ts`, `src/b/c.ts`, `src/d.js`. Assert walker returns exactly `src/a.ts` and `src/b/c.ts`.

## Implementation
`Directory.EnumerateFiles(root, "*.ts", SearchOption.AllDirectories)`.

## Done when
- Test green.
