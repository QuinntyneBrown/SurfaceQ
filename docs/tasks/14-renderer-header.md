# 14 — Renderer: header comment block

**Traces to:** L2-011

## Goal
`PublicApiRenderer.Render(empty, context)` produces an output that starts with the SurfaceQ header comment block.

## Failing test
Call render with zero file-exports. Assert output starts with `// ====` and contains `SurfaceQ` and `DO NOT EDIT`.

## Implementation
Hard-coded string constant for header; return it for empty input.

## Done when
- Test green.
