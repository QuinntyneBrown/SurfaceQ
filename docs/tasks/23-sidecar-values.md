# 23 — Sidecar: discover const, function, InjectionToken

**Traces to:** L2-008

## Goal
`export const X = ...`, `export function Y() {}`, and `export const TOKEN = new InjectionToken<...>('...')` all return as value exports.

## Failing test
Fixture with all three. Assert three value exports `X`, `Y`, `TOKEN` with `isType: false`.

## Implementation
Extend sidecar switch to `VariableStatement` (each `VariableDeclaration`'s name) and `FunctionDeclaration`.

## Done when
- Test green.
