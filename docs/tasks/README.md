# SurfaceQ Implementation Tasks

Each task below is a **vertical slice**: a tiny end-to-end behavior that can be demonstrated independently.

## Rules for every task

1. **ATDD — Write the failing test first.**
   - Create an acceptance test that exercises the behavior.
   - Run it and confirm it fails (red).
   - Write the minimum code to make it pass (green).
   - Do not refactor beyond what the test requires.
2. **Traceability.** Every test file has a header comment:
   ```csharp
   // Acceptance Test
   // Traces to: L2-00X
   // Description: ...
   ```
3. **Keep it simple.** Optimize for a reader with little C# knowledge.
   - Prefer plain classes and methods over abstractions, generics, reflection, or advanced LINQ.
   - Cyclomatic complexity per method: target ≤ 5.
   - Short methods (≤ 20 lines). Descriptive names. No clever tricks.
   - No premature interfaces; add one only when a second implementation is introduced.
4. **One task = one small PR.** Do not bundle.
5. **Update this file.** Tick the checkbox when the task's test is green and merged.

## Task list

| # | Task | Traces to | Status |
|---|---|---|---|
| 01 | [Solution skeleton + `--version`](./01-solution-skeleton-version.md) | L2-021 | [x] |
| 02 | [`--help` output](./02-help-output.md) | L2-021 | [x] |
| 03 | [Project locator: current dir](./03-project-locator-current-dir.md) | L2-004 | [x] |
| 04 | [Project locator: walk upward](./04-project-locator-walk-upward.md) | L2-004 | [x] |
| 05 | [Missing manifest → exit 2](./05-missing-manifest-exit-2.md) | L2-004 | [x] |
| 06 | [Parse `entryFile` field](./06-parse-entryfile.md) | L2-005 | [x] |
| 07 | [Fallback to `src/public-api.ts`](./07-entryfile-fallback.md) | L2-005 | [x] |
| 08 | [Malformed manifest → exit 2](./08-malformed-manifest.md) | L2-005 | [x] |
| 09 | [Missing entryFile directory → exit 2](./09-missing-entryfile-dir.md) | L2-005 | [x] |
| 10 | [File walker: basic `.ts` discovery](./10-walker-basic.md) | L2-006 | [x] |
| 11 | [Walker: exclude `*.spec.ts`, `*.stories.ts`, `index.ts`](./11-walker-exclusions.md) | L2-006 | [x] |
| 12 | [Walker: exclude `entryFile` and `node_modules`](./12-walker-entry-and-nodemodules.md) | L2-006 | [x] |
| 13 | [Walker: deterministic ordering](./13-walker-deterministic.md) | L2-007, L2-012 | [x] |
| 14 | [Renderer: header comment block](./14-renderer-header.md) | L2-011 | [x] |
| 15 | [Renderer: grouped value exports](./15-renderer-grouped-exports.md) | L2-011 | [x] |
| 16 | [Renderer: type-only exports](./16-renderer-type-exports.md) | L2-011 | [x] |
| 17 | [Renderer: POSIX specifiers + no `.ts` + relative](./17-renderer-module-specifiers.md) | L2-011 | [x] |
| 18 | [Renderer: two-space indent + trailing newline](./18-renderer-formatting.md) | L2-011 | [x] |
| 19 | [Empty scan root → header-only file](./19-empty-scan-root.md) | L2-006 | [x] |
| 20 | [Sidecar: spawn + JSON round-trip](./20-sidecar-roundtrip.md) | L2-013 | [x] |
| 21 | [Sidecar: discover class export](./21-sidecar-class.md) | L2-008 | [x] |
| 22 | [Sidecar: discover interface/type/enum](./22-sidecar-types.md) | L2-008 | [x] |
| 23 | [Sidecar: discover const/function/InjectionToken](./23-sidecar-values.md) | L2-008 | [x] |
| 24 | [Sidecar: handle `export { X } from` and `export type { X }`](./24-sidecar-reexports.md) | L2-008 | [ ] |
| 25 | [Sidecar: expand `export *`](./25-sidecar-wildcard.md) | L2-009 | [ ] |
| 26 | [Sidecar: skip default export with warning](./26-sidecar-default-export.md) | L2-010 | [ ] |
| 27 | [Sidecar: syntax error → exit 2](./27-sidecar-syntax-error.md) | L2-018 | [ ] |
| 28 | [`generate` writes file](./28-generate-writes.md) | L2-001 | [ ] |
| 29 | [`generate` overwrites existing file](./29-generate-overwrites.md) | L2-001 | [ ] |
| 30 | [`generate` with `--project` (dir and file)](./30-generate-project-flag.md) | L2-001 | [ ] |
| 31 | [`generate` read-only file → exit 2](./31-generate-readonly.md) | L2-019 | [ ] |
| 32 | [`check` matches → exit 0](./32-check-match.md) | L2-002 | [ ] |
| 33 | [`check` drift → exit 1 with concise message](./33-check-drift.md) | L2-002 | [ ] |
| 34 | [`check` error → exit 2](./34-check-error.md) | L2-002 | [ ] |
| 35 | [`diff` equal → exit 0, no output](./35-diff-equal.md) | L2-003 | [ ] |
| 36 | [`diff` differs → exit 1, unified diff](./36-diff-unified.md) | L2-003 | [ ] |
| 37 | [`diff` never writes to disk](./37-diff-no-writes.md) | L2-003 | [ ] |
| 38 | [Logging: stderr vs stdout routing](./38-logging-streams.md) | L2-016 | [ ] |
| 39 | [`--verbosity quiet` and `diagnostic`](./39-verbosity-levels.md) | L2-016 | [ ] |
| 40 | [Plain-text logs (no ANSI)](./40-plain-text-logs.md) | L2-016 | [ ] |
| 41 | [Name collisions both emitted](./41-name-collisions.md) | L2-020 | [ ] |
| 42 | [Determinism: repeat run byte-identical](./42-determinism-repeat.md) | L2-012 | [ ] |
| 43 | [No network (inspection test)](./43-no-network.md) | L2-017 | [ ] |
| 44 | [Ignore stray config files](./44-ignore-config-files.md) | L2-022 | [ ] |
| 45 | [Package as .NET global tool](./45-pack-global-tool.md) | L2-014 | [ ] |
| 46 | [Bundle Node.js per RID](./46-bundle-node.md) | L2-014 | [ ] |
| 47 | [Bundle `typescript` npm package](./47-bundle-typescript.md) | L2-014 | [ ] |
| 48 | [Cross-platform determinism CI matrix](./48-cross-platform-ci.md) | L2-007, L2-012 | [ ] |
| 49 | [Performance benchmark: 50-file cold run](./49-perf-cold.md) | L2-015 | [ ] |
| 50 | [Performance benchmark: 200-file warm run & 500-file memory](./50-perf-warm-memory.md) | L2-015 | [ ] |
