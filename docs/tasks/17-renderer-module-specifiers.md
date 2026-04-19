# 17 — Renderer: POSIX specifiers, no `.ts`, relative to entry

**Traces to:** L2-011

## Goal
Module specifiers use forward slashes, drop the `.ts` extension, and are relative to the `entryFile`'s directory, prefixed with `./` or `../`.

## Failing test
Given entry `src/public-api.ts` and source `src/lib/a.ts`, assert specifier is `./lib/a`. On Windows-style input paths, assert no backslash appears anywhere in the output.

## Implementation
Compute `Path.GetRelativePath(Path.GetDirectoryName(entry), file)`, strip `.ts`, `.Replace('\\', '/')`, prepend `./` if not starting with `.`.

## Done when
- Test green.
