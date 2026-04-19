# 22 — Sidecar: discover interface, type alias, enum

**Traces to:** L2-008

## Goal
`export interface Bar`, `export type Baz = ...`, `export enum E { A }`, and `export const enum` all appear with correct `kind` and `isType`.

## Failing test
Fixture with all four forms. Assert: interface + type alias have `isType: true`; enum and const enum have `isType: false` (they emit values).

## Implementation
Extend sidecar switch to `InterfaceDeclaration`, `TypeAliasDeclaration`, `EnumDeclaration`.

## Done when
- Test green.
