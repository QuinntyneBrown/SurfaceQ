# 11 — Walker excludes `*.spec.ts`, `*.stories.ts`, `index.ts`

**Traces to:** L2-006

## Goal
Walker omits spec, stories, and any file literally named `index.ts`.

## Failing test
Fixture contains `a.ts`, `a.spec.ts`, `a.stories.ts`, `sub/index.ts`, `b.ts`. Assert walker returns exactly `a.ts` and `b.ts`.

## Implementation
Filter by filename: `!name.EndsWith(".spec.ts") && !name.EndsWith(".stories.ts") && name != "index.ts"`.

## Done when
- Test green. Task 10 still passes.
