# 15 — Renderer: grouped value exports

**Traces to:** L2-011

## Goal
Symbols from one source file appear as a single `export { A, B, C } from '<path>';` statement, in source order.

## Failing test
Input: one `FileExports` with value exports `A`, `B`, `C`. Assert output contains exactly `export { A, B, C } from './x';` (no duplicate statements).

## Implementation
For each `FileExports`, join value-export names with `", "` inside `export { ... } from '...';`.

## Done when
- Test green. Task 14 still passes.
