# 25 — Sidecar: expand `export *`

**Traces to:** L2-009

## Goal
`export * from './inner'` is resolved via the TS Compiler API into the actual named symbols and emitted explicitly.

## Failing test
Fixture: `a.ts` contains `export * from './inner';`; `inner.ts` exports `A` and `B`. Run full `generate`. Assert final `public-api.ts` contains `A` and `B` but zero occurrences of `export *`.

## Implementation
Use `TypeChecker.getExportsOfModule(sourceFile.symbol)` inside the sidecar. Map each symbol back to the file where it is declared.

## Done when
- Test green. Renderer still groups by declaring file.
