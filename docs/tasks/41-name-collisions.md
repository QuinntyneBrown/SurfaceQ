# 41 — Name collisions both emitted

**Traces to:** L2-020

## Goal
When two files each export a symbol named `Foo`, both statements appear in the output.

## Failing test
Fixture with `a.ts` (`export class Foo`) and `b.ts` (`export class Foo`). Run `generate`. Assert output contains `export { Foo } from './a';` AND `export { Foo } from './b';`.

## Implementation
No adjudication; the renderer simply emits what it receives per file.

## Done when
- Test green.
