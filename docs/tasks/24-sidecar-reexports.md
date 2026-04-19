# 24 — Sidecar: `export { X } from` and `export type { X }`

**Traces to:** L2-008

## Goal
Explicit named re-exports are included and type-only re-exports preserve their type-only flag.

## Failing test
Fixture `a.ts` has `export { Foo } from './b';` and `export type { Bar } from './b';`. Assert sidecar reports `Foo` (value) and `Bar` (type) as exports of `a.ts`.

## Implementation
Handle `ExportDeclaration` nodes with a module specifier; read `isTypeOnly` on the declaration and on each element.

## Done when
- Test green.
