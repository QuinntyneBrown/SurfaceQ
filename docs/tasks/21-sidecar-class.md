# 21 — Sidecar: discover class export

**Traces to:** L2-008

## Goal
Given a TS file with `export class Foo {}`, `discover` returns `{ name: "Foo", kind: "class", isType: false }`.

## Failing test
Integration test writes the fixture file, calls `discover`, asserts the single export.

## Implementation
In `sidecar.js`, use `ts.createProgram` / `ts.forEachChild`; collect exported `ClassDeclaration` nodes.

## Done when
- Test green.
