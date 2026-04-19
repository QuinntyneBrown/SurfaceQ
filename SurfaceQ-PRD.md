# SurfaceQ — Product Requirements Document

**Explicit Public API Generator for Angular Libraries**

| | |
|---|---|
| **Product** | SurfaceQ |
| **Suite** | Q-Suite |
| **Author** | Quinn Brown |
| **Status** | Draft v1.0 |
| **Date** | April 19, 2026 |
| **Distribution** | NuGet (.NET global tool) |
| **Runtime** | .NET 8.0+ (with bundled Node.js sidecar) |
| **Primary Target** | Angular 17 (forward-compatible) |
| **License** | TBD |
| **Repository** | github.com/QuinntyneBrown/SurfaceQ |

---

## 1. Overview

### 1.1 Problem Statement

Angular libraries built with `ng-packagr` require a `public-api.ts` entry file that re-exports every consumable symbol. The prevailing convention across the Angular ecosystem is to use wildcard re-exports (`export * from './path'`), which is concise but produces several undesirable outcomes:

- **Loss of intentionality** — every exported symbol from a file becomes part of the public API by accident, including helpers and internal utilities never meant for consumers.
- **Poor tree-shaking signals** — bundlers and IDEs cannot reason as effectively about what is actually consumed when wildcards are layered.
- **Hidden API surface drift** — a developer adds an export to a deep file and inadvertently expands the library's public contract with no review signal.
- **Weaker documentation generation** — tools like Compodoc and TypeDoc produce noisier output when the surface is not curated.
- **Larger code-review blind spots** — wildcard barrels obscure what changed in a PR's public contract.

Maintaining a hand-written, explicit `public-api.ts` solves these problems but is tedious, error-prone, and difficult to keep in sync as a library grows.

### 1.2 Vision

SurfaceQ is a .NET CLI tool, distributed via NuGet, that scans an Angular library project and generates a fully explicit `public-api.ts` file using named re-exports of the form:

```typescript
export { Foo, Bar, Baz } from './path/to/file';
```

It eliminates wildcard re-exports while removing the manual labor of maintaining the file by hand. SurfaceQ is opinionated, deterministic, and CI-friendly.

### 1.3 Target Users

- Angular library authors maintaining publishable packages (npm or private registries).
- Enterprise teams maintaining internal monorepo libraries who require strict API boundary controls.
- Architects enforcing API governance across multiple Angular libraries.
- CI/CD pipelines that need a verifiable, drift-free public surface.

### 1.4 Non-Goals

- SurfaceQ does not transform, lint, or format library source code; it only generates the entry file.
- SurfaceQ is not a build tool replacement for `ng-packagr`.
- SurfaceQ does not enforce semantic versioning rules or perform breaking-change analysis (a candidate for a future Q-tool).
- SurfaceQ does not modify `ng-package.json`, `tsconfig.json`, or any other project configuration files.
- SurfaceQ does not classify symbols as public versus internal — every discovered named export is treated as public.
- SurfaceQ does not handle non-Angular TypeScript libraries as a v1 priority (though it should work for them incidentally).

---

## 2. Goals & Success Criteria

### 2.1 Primary Goals

- Produce a deterministic, explicit `public-api.ts` from an Angular 17+ library with zero hand-editing.
- Run as a single self-contained .NET global tool with no required external runtime dependencies for the user.
- Integrate cleanly into CI pipelines through a `check` subcommand that exits non-zero on drift.
- Provide accurate parsing fidelity by leveraging the official TypeScript Compiler API rather than ad-hoc parsing.

### 2.2 Success Metrics

| Metric | Target |
|---|---|
| Cold-start runtime on a 50-file library | Under 5 seconds |
| Warm runtime on a 200-file library | Under 10 seconds |
| False positives (symbols included that shouldn't be) | Zero on canonical Angular 17 layouts |
| False negatives (named exports missed) | Zero on canonical Angular 17 layouts |
| Determinism | Byte-identical output on repeated runs against unchanged source |
| Install footprint (NuGet package size) | Under 60 MB including bundled Node runtime |
| CI integration friction | Single command, single non-zero exit code on drift |

---

## 3. Functional Requirements

### 3.1 Subcommands

SurfaceQ exposes three top-level subcommands implemented with `System.CommandLine`.

#### 3.1.1 `generate`

Scans the library, discovers named exports, and writes (overwrites) the `public-api.ts` file in the location specified by `ng-package.json`'s `entryFile` property. This is the primary command for development workflows.

```bash
surfaceq generate [--project <path>]
```

- **Default behavior:** operates against the current working directory; locates `ng-package.json` by walking upward; resolves `entryFile` relative to `ng-package.json`'s directory; writes the generated file at that location; exits `0` on success.
- **`--project <path>`** (optional): explicit path to the project directory or directly to an `ng-package.json` file.

#### 3.1.2 `check`

Runs the same discovery and generation logic as `generate`, but instead of writing to disk it compares the would-be output against the existing `public-api.ts`. Exits non-zero if they differ. Designed for CI pipelines.

```bash
surfaceq check [--project <path>]
```

- **Exit codes:**
  - `0` — file matches expected output.
  - `1` — drift detected (file differs from expected output).
  - `2` — error during execution (parse failure, missing `ng-package.json`, etc.).
- Prints a concise message identifying that drift was detected, but does not print the full diff (that is the `diff` command's job).

#### 3.1.3 `diff`

Same discovery logic, but emits a human-readable unified diff between the existing `public-api.ts` and the would-be generated output. Does not write to disk. Useful for previewing changes before committing.

```bash
surfaceq diff [--project <path>]
```

- **Exit codes:**
  - `0` — no diff (file matches).
  - `1` — diff present (printed to stdout).
  - `2` — error.

### 3.2 Project Discovery

- SurfaceQ locates the project by searching for `ng-package.json` starting at the working directory (or the path supplied to `--project`) and walking upward through parent directories until found or the filesystem root is reached.
- The `entryFile` field of `ng-package.json` (default: `"src/public-api.ts"`) determines both the output path and the directory whose `.ts` files will be scanned.
- The directory containing the resolved `entryFile` is the **scan root**.
- If no `ng-package.json` is found, SurfaceQ exits with code `2` and a clear error message.
- If `ng-package.json` exists but has no `entryFile` field, SurfaceQ falls back to the ng-packagr default of `src/public-api.ts` and logs an informational message.

### 3.3 Source File Discovery

- SurfaceQ recursively walks all `.ts` files under the scan root.
- The following files are excluded from scanning:
  - `*.spec.ts`
  - `*.stories.ts`
  - `index.ts` (any file literally named `index.ts`)
  - The `entryFile` itself (the file being generated).
  - Any file inside a `node_modules` directory.
- File walk order is **deterministic** — sorted by relative path using ordinal (case-insensitive on Windows, case-sensitive on Linux/macOS, but normalized for consistent output).

### 3.4 Symbol Discovery & Classification

SurfaceQ delegates parsing to a bundled Node.js sidecar that runs the TypeScript Compiler API. The sidecar reports back a structured list of named exports per file. SurfaceQ recognizes and includes the following export kinds:

| Category | Examples |
|---|---|
| Angular building blocks | `@Component`, `@Directive`, `@Pipe`, `@Injectable`, `@NgModule` |
| Standalone primitives | Standalone components, functional `CanActivateFn`, `HttpInterceptorFn`, `ResolveFn`, etc. |
| Type system | `interface`, `type` aliases, `enum`, `const enum` |
| Values & DI | Exported `const`, `let`, `var`; `InjectionToken` instances |
| Functions & classes | All exported `function` declarations and `class` declarations not covered above |
| Re-exports | `export { X } from './sub-barrel'` style declarations within scanned files |
| Type-only exports | `export type { Foo }` is preserved as a type-only re-export |

All named exports are treated as public. Internal/private classification is out of scope for v1.

### 3.5 Default Export Handling

Default exports cannot be cleanly re-exported in the form `export { X } from '...'`. When SurfaceQ encounters a `export default` declaration:

- The default export is **skipped**.
- A warning is logged to stderr identifying the file and indicating the default export was excluded.
- The run continues normally and exits `0` (warnings do not fail the run).

Rationale: Angular libraries overwhelmingly use named exports; defaults are rare and typically indicate non-idiomatic code. SurfaceQ's stance is "log it, skip it, move on."

### 3.6 Output Format

The generated `public-api.ts` follows these rules:

- A header comment block at the top of the file, identifying SurfaceQ as the generator and warning against manual edits.
- Symbols from the same source file are **grouped into a single `export { ... } from '...'` statement**.
- Files are emitted in the **order they were discovered** during the recursive file walk (sorted by relative path).
- Within a single file's export statement, symbols are ordered as they appear in the source file (preserving authorial intent).
- Type-only re-exports use `export type { ... } from '...'`.
- Mixed type and value exports from the same file produce two adjacent statements: a value `export { ... }` and a type `export type { ... }`.
- Module specifiers use POSIX-style forward slashes regardless of host OS.
- Module specifiers are written as relative paths from the entry file's directory, with no `.ts` extension.
- A trailing newline is present at end of file.
- Indentation uses two spaces (matching Angular style).

#### 3.6.1 Sample Output

```typescript
// =============================================================================
// THIS FILE IS GENERATED BY SurfaceQ. DO NOT EDIT BY HAND.
// Regenerate with:  surfaceq generate
// Verify in CI:     surfaceq check
// =============================================================================

export { ButtonComponent, ButtonVariant } from './lib/button/button.component';
export { ButtonModule } from './lib/button/button.module';
export type { ButtonConfig } from './lib/button/button.types';
export { DialogService } from './lib/dialog/dialog.service';
export { DIALOG_DEFAULT_CONFIG } from './lib/dialog/dialog.tokens';
export { provideDialog } from './lib/dialog/dialog.providers';
```

### 3.7 Logging

- Plain-text logging only (no JSON, no rich formatting, no colors).
- Implemented via `Microsoft.Extensions.Logging` with a console provider.
- Default verbosity: `Information`. Adjustable via global `--verbosity <level>` flag (`quiet`, `minimal`, `normal`, `detailed`, `diagnostic`), mirroring familiar `dotnet` CLI conventions.
- Errors and warnings always go to **stderr**; informational and verbose output go to **stdout**.

### 3.8 Configuration

- v1 supports **no project-level config file**. All behavior is driven by `ng-package.json` auto-detection plus CLI flags.
- This is a deliberate constraint to keep the v1 surface small. A future `surfaceq.config.json` may be added in v2 if real user demand emerges.

---

## 4. Non-Functional Requirements

### 4.1 Platform & Runtime

- **Built on:** .NET 8.0 SDK.
- **Runs on:** .NET 8.0 or higher runtimes (forward-compatible with .NET 9, .NET 10).
- **Distribution:** NuGet package as a .NET global tool (`<PackAsTool>true</PackAsTool>`).
- **OS support:** Windows (x64, ARM64), Linux (x64, ARM64), macOS (x64, ARM64).
- **Install command:** `dotnet tool install --global SurfaceQ`.

### 4.2 Bundled Node.js Sidecar

- A Node.js runtime is bundled inside the NuGet package — the user does not need Node.js installed.
- Per-RID Node binaries are included to keep package size bounded; .NET runtime identifier resolution selects the correct binary at startup.
- The TypeScript Compiler API (`typescript` npm package) ships pre-bundled with the sidecar — no `npm install` is performed at runtime.
- Sidecar communication uses stdin/stdout JSON-RPC framing for simplicity and cross-platform reliability.
- Sidecar lifetime is scoped to a single CLI invocation.

### 4.3 Performance

- Cold-start (process launch + sidecar spawn + scan of 50-file library) under 5 seconds on a modern developer laptop.
- Warm scan of a 200-file library under 10 seconds.
- Memory ceiling under 512 MB for libraries up to 500 files.

### 4.4 Determinism

- Identical input must produce byte-identical output across runs, hosts, and operating systems (path separators normalized, file ordering normalized).
- This is essential for `check` to be reliable in CI.

### 4.5 Observability

- All output is plain text.
- Warnings (e.g., skipped default exports) are clearly prefixed with `warn:` and identify the offending file with a relative path.
- Errors include enough context to diagnose without re-running with `--verbosity diagnostic`.

### 4.6 Security

- SurfaceQ does not network — no telemetry, no remote calls, no update checks.
- The Node sidecar is launched with no arguments derived from user-provided file content; only file paths are passed, and the sidecar reads files itself.
- No shell invocation; the sidecar is launched via `Process.Start` with explicit arguments.

---

## 5. Architecture

### 5.1 High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────┐
│                    SurfaceQ CLI (.NET 8)                │
│  ┌──────────────────────────────────────────────────┐   │
│  │   System.CommandLine root                        │   │
│  │   ├─ generate                                    │   │
│  │   ├─ check                                       │   │
│  │   └─ diff                                        │   │
│  └──────────────────────────────────────────────────┘   │
│                          │                              │
│  ┌───────────────────────▼──────────────────────────┐   │
│  │   IHost (Microsoft.Extensions.Hosting)           │   │
│  │   - DI container                                 │   │
│  │   - Logging                                      │   │
│  └───────────────────────┬──────────────────────────┘   │
│                          │                              │
│  ┌──────────┬────────────┼──────────────┬───────────┐   │
│  │ Project  │ FileWalker │ ExportEngine │ Renderer  │   │
│  │ Locator  │            │              │           │   │
│  └──────────┴────────────┴──────┬───────┴───────────┘   │
│                                 │                       │
│                  ┌──────────────▼────────────┐          │
│                  │  Node Sidecar Manager     │          │
│                  │  (process + stdio + JSON) │          │
│                  └──────────────┬────────────┘          │
└─────────────────────────────────┼───────────────────────┘
                                  │
                  ┌───────────────▼─────────────────┐
                  │    Node.js (bundled)            │
                  │    + TypeScript Compiler API    │
                  │    surfaceq-sidecar.js          │
                  └─────────────────────────────────┘
```

### 5.2 Solution Layout

```
SurfaceQ/
├─ src/
│  ├─ SurfaceQ.Cli/             # Entry point, System.CommandLine wiring
│  ├─ SurfaceQ.Core/            # Discovery, rendering, comparison
│  ├─ SurfaceQ.Sidecar/         # Sidecar manager + JSON contracts
│  └─ SurfaceQ.Sidecar.Node/    # Node sidecar (TS source, bundled at build)
└─ test/
   ├─ SurfaceQ.Core.Tests/
   ├─ SurfaceQ.Cli.Tests/
   └─ SurfaceQ.Integration.Tests/
```

### 5.3 Key Interfaces

```csharp
public interface IProjectLocator
{
    ProjectContext Locate(string startPath);
}

public interface ISourceFileWalker
{
    IReadOnlyList<SourceFile> Walk(ProjectContext context);
}

public interface IExportDiscoveryEngine
{
    Task<IReadOnlyList<FileExports>> DiscoverAsync(
        IReadOnlyList<SourceFile> files,
        CancellationToken ct);
}

public interface IPublicApiRenderer
{
    string Render(IReadOnlyList<FileExports> exports, ProjectContext context);
}

public interface IPublicApiComparer
{
    ComparisonResult Compare(string expected, string actual);
}
```

### 5.4 Sidecar Contract

JSON over stdio. Newline-delimited JSON messages (one JSON object per line) for simplicity.

**Request (CLI → Sidecar):**

```json
{
  "id": "req-1",
  "method": "discover",
  "params": {
    "files": ["/abs/path/file1.ts", "/abs/path/file2.ts"],
    "projectRoot": "/abs/path"
  }
}
```

**Response (Sidecar → CLI):**

```json
{
  "id": "req-1",
  "result": {
    "files": [
      {
        "path": "/abs/path/file1.ts",
        "exports": [
          { "name": "ButtonComponent", "kind": "class", "isType": false },
          { "name": "ButtonConfig",    "kind": "interface", "isType": true }
        ],
        "warnings": [
          { "kind": "default-export-skipped", "message": "Default export at line 42 was skipped" }
        ]
      }
    ]
  }
}
```

### 5.5 .NET Dependencies

- `System.CommandLine` (latest stable beta or GA)
- `Microsoft.Extensions.Hosting`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`
- `Microsoft.Extensions.Logging.Console`
- `System.Text.Json` (BCL, for sidecar JSON-RPC)

No third-party logging, JSON, or DI libraries — Microsoft Extensions only, in keeping with Q-Suite consistency.

### 5.6 Node Sidecar Dependencies

- `typescript` (pinned to a known-good version aligned with Angular 17's TS range)
- No other npm packages — the sidecar is intentionally minimal.

---

## 6. CLI Reference

### 6.1 Global Options

| Option | Description |
|---|---|
| `--verbosity <level>` | One of `quiet`, `minimal`, `normal` (default), `detailed`, `diagnostic` |
| `--version` | Print SurfaceQ version and exit |
| `--help` / `-h` | Print help text |

### 6.2 Command Reference

| Command | Description | Exit Codes |
|---|---|---|
| `surfaceq generate [--project <path>]` | Write `public-api.ts` to disk | `0` success, `2` error |
| `surfaceq check [--project <path>]` | Verify file matches expected | `0` match, `1` drift, `2` error |
| `surfaceq diff [--project <path>]` | Print unified diff to stdout | `0` no diff, `1` diff, `2` error |

### 6.3 Example CI Integration (GitHub Actions)

```yaml
- name: Verify public API surface
  run: |
    dotnet tool install --global SurfaceQ
    surfaceq check --project libs/my-lib
```

---

## 7. Edge Cases & Error Handling

| Scenario | Behavior |
|---|---|
| No `ng-package.json` found | Exit `2`, error message identifies search path |
| `ng-package.json` is malformed JSON | Exit `2`, error message identifies file and parse error |
| `entryFile` field missing | Fall back to `src/public-api.ts`, log info |
| `entryFile` directory does not exist | Exit `2`, error message |
| Scan root contains zero `.ts` files (after exclusions) | Exit `0`, write a header-only `public-api.ts`, log info |
| File contains syntax errors | Sidecar reports diagnostic; SurfaceQ exits `2` and prints offending file/line |
| File contains only `export default` | File contributes no exports; default-skip warning logged |
| File contains `export *` (wildcard re-export) | Resolved by the TS Compiler API to the underlying named symbols and emitted explicitly; this is the core SurfaceQ value-add |
| `public-api.ts` is read-only | Exit `2` with permissions error |
| Same name exported from two files | Both exports are emitted; downstream TS compilation will fail naturally — SurfaceQ does not adjudicate name collisions in v1 |
| Cyclic re-exports | TS Compiler API handles resolution; SurfaceQ records exports as discovered |
| Symbol exported only as type vs. as value | Emitted with correct `export` vs. `export type` syntax |

---

## 8. Out of Scope (v1) / Future Considerations

The following are explicitly **not** in v1 but are tracked as candidates for future versions:

- Project-level `surfaceq.config.json` for include/exclude globs and formatting overrides.
- JSDoc-tag-driven public/internal classification (`@public`, `@internal`).
- Path-convention-driven exclusion (e.g., omit files under `/internal/`).
- Watch mode (`surfaceq watch`) for live regeneration during development.
- Multi-entry-point support (Angular libraries with secondary entry points).
- Breaking-change detection between commits (a hypothetical companion: **DriftQ**).
- Integration with TraceQ for requirements-to-API traceability.
- Plugin model for custom rendering strategies.
- VS Code extension wrapping the CLI.

---

## 9. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| TypeScript version drift breaks sidecar | High | Pin TS version in sidecar; add CI matrix testing against Angular 17, 18, 19, 20 |
| Bundled Node.js inflates package size | Medium | Use Node `--single-executable-application` or per-RID slim builds; document size in release notes |
| Performance regressions on large libraries | Medium | Establish benchmark suite in repo; track on each PR |
| Determinism violations across OSes | High | Path normalization tests in cross-platform CI matrix (Windows/Linux/macOS) |
| Angular evolves away from `ng-package.json` | Low | Convention is stable as of Angular 17; revisit at each major Angular release |
| User has both `public-api.ts` and `public_api.ts` (legacy underscore) | Low | Honor `entryFile` exactly as written in `ng-package.json`; document this behavior |

---

## 10. Release Plan

### 10.1 Milestones

| Milestone | Scope |
|---|---|
| **M1 — Walking skeleton** | .NET CLI shell with `System.CommandLine`; Node sidecar spawn and round-trip JSON; `--version` works |
| **M2 — Discovery engine** | Project locator + file walker + sidecar discovery returning structured exports for a small fixture library |
| **M3 — Renderer** | Output formatting matching §3.6; deterministic ordering verified |
| **M4 — `generate` command** | End-to-end generation against fixture libraries; default-export warning behavior |
| **M5 — `check` and `diff` commands** | CI-ready verification; unified diff output |
| **M6 — Packaging** | NuGet packaging as global tool with bundled Node per RID |
| **M7 — v1.0 release** | Documentation, README, GitHub Actions example, NuGet publish |

### 10.2 Versioning

- Semantic versioning (`MAJOR.MINOR.PATCH`).
- v1.0 is the first public NuGet release.
- Breaking changes to CLI surface or output format require a major version bump.

---

## 11. Open Questions

1. Should the generated header comment include the SurfaceQ version that produced it? (Tradeoff: traceability vs. extra churn on tool upgrades.)
2. Should `check` support a `--diff-on-fail` flag to print the diff without requiring a second `diff` invocation?
3. Should the NuGet package ship a single multi-RID binary (larger) or separate per-RID packages (more downloads to maintain)?
4. Should there be a `surfaceq init` command that adds an `npm script` and a CI snippet to nudge first-time users toward the right workflow?

---

## 12. Appendix

### 12.1 Glossary

- **Barrel file** — a single TypeScript file that re-exports symbols from other files; `public-api.ts` is the barrel for an Angular library.
- **`ng-package.json`** — the manifest consumed by `ng-packagr` describing how to build an Angular library.
- **`entryFile`** — the `ng-package.json` field naming the barrel file.
- **Wildcard re-export** — `export * from './path'`; the convention SurfaceQ replaces.
- **Named re-export** — `export { Foo, Bar } from './path'`; the convention SurfaceQ produces.
- **Drift** — a state where the on-disk `public-api.ts` no longer matches what SurfaceQ would generate.

### 12.2 References

- ng-packagr documentation: <https://github.com/ng-packagr/ng-packagr>
- TypeScript Compiler API: <https://github.com/microsoft/TypeScript/wiki/Using-the-Compiler-API>
- System.CommandLine: <https://learn.microsoft.com/en-us/dotnet/standard/commandline/>
- .NET global tools: <https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools>
