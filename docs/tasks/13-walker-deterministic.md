# 13 — Walker: deterministic ordering

**Traces to:** L2-007, L2-012

## Goal
Walker returns files sorted by relative path using a stable, cross-platform comparison (ordinal).

## Failing test
Fixture with several files. Invoke walker twice; assert both sequences are identical. Assert order equals `OrderBy(relPath, StringComparer.Ordinal)`.

## Implementation
Project each file to a relative path, `.OrderBy(x => x, StringComparer.Ordinal).ToList()`.

## Done when
- Test green.
