# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

SurfaceQ is a .NET 8 global tool (`surfaceq`) that generates an explicit `public-api.ts` for Angular libraries by scanning `.ts` sources beneath the `entryFile` declared in `ng-package.json`. It emits one `export { … } from './…'` per declaring module (values first, then a separate `export type { … }` line), expands wildcard re-exports, rejects default exports with a warning, and produces byte-identical output across Windows/Linux/macOS.

Commands, with meaningful exit codes that CI relies on:
- `generate` — write `public-api.ts` (exit `0` ok, `2` error)
- `check` — verify on-disk matches expected, no write (exit `0` match, `1` drift, `2` error)
- `diff` — print unified diff, no write (exit `0` match, `1` differ, `2` error)
- `docs` — document every library in a workspace as Markdown, one `API.md` per library (exit `0` ok, `2` error). `--project` is the workspace root; `--output <path>` (default `API.md`) is relative to each library directory. By default it hides classes that implement an interface exported by the same library (implementation details reached via injection token); `--include-implementations` shows them. Filtering lives in `DocumentationPipeline.ExcludeImplementations`. Declarations and members tagged with the `@deprecated` JSDoc tag get a `Deprecated` column (reason / `yes` / `no`), a callout under heading-rendered interfaces/classes/enums, and a `Deprecations` summary table at the top when anything is deprecated.
- `inventory` — a complete census of every developer-authored object (exported **and** internal) in an Angular app, library, or workspace, one `INVENTORY.md` per project (exit `0` ok, `2` error). `--project` is the workspace root; `--output <path>` (default `INVENTORY.md`) is relative to each project directory. Unlike `docs` (libraries only, public contract), `inventory` also discovers **applications** via `angular.json`/`project.json` (`WorkspaceProjectLocator`), lists internal declarations, and classifies each object by **Angular role** (Component/Directive/Pipe/Service/NgModule/Guard/Resolver/Interceptor) detected in the sidecar's `inventory` method. Excludes tests (`*.spec.ts`, `*.stories.ts`, `*.e2e-spec.ts`, `*.d.ts`, `*-e2e` projects), `node_modules`, and `dist`. It is a census — no member detail, no reachability tracing, no implementation hiding.

## Build & test

Requires **.NET 8 SDK** (pinned to 8.0.420 in `global.json`) and **Node.js 22**.

```sh
npm ci --prefix src/SurfaceQ.Sidecar.Node   # seed pinned typescript tree (one-time)
dotnet build
dotnet test
```

```sh
dotnet test --filter "Category!=Performance"   # skip slow perf benchmarks
dotnet test --filter "Category=Performance"     # only perf benchmarks
dotnet test --filter "FullyQualifiedName~PublicApiRendererFormattingTests"   # single test class
```

Pack & install the global tool locally:

```sh
dotnet pack src/SurfaceQ.Cli -c Release -o ./artifacts
dotnet tool install --global --add-source ./artifacts SurfaceQ
```

## Architecture

The system is a **.NET host driving a long-lived Node sidecar** over line-delimited JSON-RPC on stdin/stdout. The host owns file I/O, grouping, ordering, and rendering; the sidecar owns the TypeScript compiler API. This split keeps the TypeScript dependency out of .NET and keeps the host unit-testable.

Data flow (`OutputPipeline.Build` is the orchestrator — read this first):

1. `ProjectLocator` finds `ng-package.json` (from `--project`, or walks upward from cwd).
2. `ManifestReader` reads the single `entryFile` field (defaults to `src/public-api.ts`), producing a `ProjectContext`.
3. `SourceFileWalker` enumerates `*.ts` under the scan root, **excluding** the entry file, `node_modules/`, `index.ts`, `*.spec.ts`, `*.stories.ts`; returns paths sorted by `Ordinal` relative path for determinism.
4. For each file, `SidecarClient` sends a `discover` JSON-RPC request to `sidecar.js`, which parses the file with `ts.createSourceFile` and returns `{ exports, warnings, errors }`. Exports are grouped by declaring file and deduped; `isType` separates value vs type re-exports. A parse error in any file aborts with exit 2.
5. `PublicApiRenderer` emits the final string. **Its output is a format contract** — header wording, two-space joins, POSIX `./`-prefixed specifiers without `.ts`, LF endings, single trailing newline. `test/SurfaceQ.Core.Tests/PublicApiRendererFormattingTests.cs` locks this; don't change formatting without updating that test.

The **`docs` command** is a second, parallel pipeline (`DocsCommand` → `DocumentationPipeline`): `WorkspaceLocator` finds all `ng-package.json` under the workspace, then for each library it reuses `SourceFileWalker` but sends the richer **`document`** JSON-RPC method (interface members + return types, enum members, type-alias definitions, `InjectionToken<T>` contract types, JSDoc). `MarkdownRenderer` turns the resulting `LibraryApi` (records in `ApiDoc.cs`) into tables. Like the generate path, it documents every exported declaration in every walked file (no reachability tracing from the entry file). The sidecar walks one file per request; the host owns workspace discovery, ordering, and rendering.

The **`inventory` command** is a third pipeline (`InventoryCommand` → `InventoryPipeline`): `WorkspaceProjectLocator` discovers every application **and** library (via `angular.json`/`project.json`/`ng-package.json`, with a single-target fallback), then `InventoryWalker` enumerates each project's `.ts` (minus tests/stories/`.d.ts`/`node_modules`/`dist`) and the sidecar's **`inventory`** method reports *every* top-level declaration — exported or not — tagged with an Angular role. `InventoryRenderer` groups the resulting `ProjectInventory` (records in `Inventory.cs`) by role. All three pipelines share `OutputPipeline.ResolveSidecarScript`.

### Projects

- `src/SurfaceQ.Core` — pure logic, no Node: `ProjectLocator`, `ManifestReader`, `SourceFileWalker`, `PublicApiRenderer`, the docs side (`WorkspaceLocator`, `ApiDoc` model records, `MarkdownRenderer`), and the inventory side (`WorkspaceProjectLocator` for apps+libs, `InventoryWalker`, `Inventory` model records, `InventoryRenderer`).
- `src/SurfaceQ.Cli` — `System.CommandLine` entry point (`Program.cs`), command handlers (`GenerateCommand`/`CheckCommand`/`DiffCommand`/`DocsCommand`/`InventoryCommand`), `OutputPipeline`/`DocumentationPipeline`/`InventoryPipeline` (orchestration), verbosity `Writers`, `NodeResolver`, `UnifiedDiff`. Assembly name is `surfaceq`.
- `src/SurfaceQ.Sidecar` — thin `SidecarClient` that spawns `node` and does `WriteLine`/`ReadLine`.
- `src/SurfaceQ.Sidecar.Node` — `sidecar.js` plus the pinned `typescript` dependency (vendored under `node_modules/`).

### Sidecar resolution & packaging (the tricky part)

- **Dev runs use the system `node`** (`SidecarClient` hardcodes `FileName = "node"`); `OutputPipeline.ResolveSidecarScript` finds `sidecar.js` by checking `content/sidecar/sidecar.js` next to the binary first, then walking upward to `src/SurfaceQ.Sidecar.Node/sidecar.js`.
- **Packaged runs** bundle both `sidecar.js` (+ the typescript tree) and platform Node binaries under `content/node/<rid>/`. `NodeResolver` maps the runtime to a RID (`win`/`linux`/`osx` × `x64`/`arm64`).
- The CLI csproj runs `npm ci --omit=dev` before pack if the typescript tree is missing, and packs the sidecar into `tools/net8.0/any/content/sidecar/`.

## Conventions (from CONTRIBUTING.md)

- **ATDD, test-first, one vertical slice per PR.** Every acceptance test carries a header tracing it to an L2 requirement (`docs/specs/L2.md`):
  ```csharp
  // Acceptance Test
  // Traces to: L2-00X
  // Description: ...
  ```
  Write the failing test first; if it passes immediately, tighten it.
- **Optimize for readers new to C#.** Plain classes/methods over abstractions, generics, reflection, or clever LINQ. No premature interfaces (add one only at a second implementation). Methods ≤ 20 lines, cyclomatic complexity ≤ 5. Comment only the *why*.
- **No trailing whitespace, LF line endings, UTF-8 without BOM.**
- Commits: imperative ≤ 72 chars; prefix roadmap work with `Task NN:`.

## Behavior contracts not to break

- **Determinism** — same inputs must produce byte-identical output across hosts and runs (ordering is `Ordinal`, paths normalized to POSIX).
- **No network** at build or runtime.
- **No config file** — behavior is fixed in v1; reads only `entryFile` from `ng-package.json`.
- Exit codes per command (above) are part of the CI contract.
