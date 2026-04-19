# 12 — Walker excludes `entryFile` and `node_modules`

**Traces to:** L2-006

## Goal
Walker omits the `entryFile` itself and any file inside a `node_modules` directory at any depth.

## Failing test
Fixture includes `src/public-api.ts` (entry) and `src/node_modules/foo/bar.ts`, plus a normal `src/a.ts`. Assert walker returns only `src/a.ts`.

## Implementation
Add two filters: path != entry, and path segments do not contain `node_modules`.

## Done when
- Test green. Tasks 10–11 still pass.
