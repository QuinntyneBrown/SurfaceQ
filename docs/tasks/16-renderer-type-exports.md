# 16 — Renderer: type-only and mixed exports

**Traces to:** L2-011

## Goal
Type-only exports render as `export type { ... } from '...';`. A file with both value and type exports emits two adjacent statements, value first.

## Failing test
Input: one file with values `X` and types `Y`. Assert output contains, in order: `export { X } from './f';` then `export type { Y } from './f';`.

## Implementation
For each file, emit value line (if any), then type line (if any).

## Done when
- Test green. Tasks 14–15 still pass.
