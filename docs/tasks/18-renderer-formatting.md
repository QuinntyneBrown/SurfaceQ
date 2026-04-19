# 18 — Renderer: indentation and trailing newline

**Traces to:** L2-011

## Goal
Output uses two-space indentation for any multi-line content and ends with exactly one `\n`.

## Failing test
Assert `output.EndsWith("\n") && !output.EndsWith("\n\n")`. Assert no tab characters. If multi-line statements are emitted, assert each indent level is exactly two spaces.

## Implementation
Use `"\n"` line endings (not `Environment.NewLine`) throughout the renderer. Append one final `"\n"`.

## Done when
- Test green.
