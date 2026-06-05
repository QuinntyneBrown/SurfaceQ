# SurfaceQ

[![CI](https://github.com/QuinntyneBrown/SurfaceQ/actions/workflows/ci.yml/badge.svg)](https://github.com/QuinntyneBrown/SurfaceQ/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SurfaceQ.svg)](https://www.nuget.org/packages/SurfaceQ)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE)

**SurfaceQ** is an explicit public API generator for Angular libraries.

It scans the `.ts` sources beneath your `ng-package.json` entry file and emits a fully explicit `public-api.ts` — one named `export { … } from './…'` statement per source module, with type-only re-exports preserved. Wildcard re-exports are expanded to their underlying symbols, default exports are rejected with a warning, and the output is byte-identical across Windows, Linux, and macOS.

## Why SurfaceQ

- **Explicit beats implicit.** A barrel of `export * from '…'` hides which symbols are actually public. SurfaceQ produces a list you can read, review, and enforce in CI.
- **Deterministic.** Same inputs produce byte-identical output on every host and every run.
- **Drop-in for `ng-packagr`.** Reads the same `ng-package.json` your library already uses; writes to the same `entryFile` path.
- **Offline.** No network access at build or runtime. Node.js and TypeScript are bundled into the NuGet package.
- **No config file.** Behavior is fixed in v1; upstream tooling does not need a new schema to learn.

## Install

SurfaceQ ships as a .NET global tool. Requires the .NET 8 runtime.

```sh
dotnet tool install --global SurfaceQ
```

Or install into a project-scoped tool manifest:

```sh
dotnet new tool-manifest       # once per repo
dotnet tool install SurfaceQ
```

## Quick start

From your Angular library directory (the one containing `ng-package.json`):

```sh
surfaceq generate
```

That writes `src/public-api.ts` (or whatever `entryFile` is declared in your manifest) with a generated header and an explicit list of re-exports.

## Commands

| Command | Purpose | Writes? | Exit codes |
|---|---|---|---|
| `generate` | Produce `public-api.ts` and write it to disk. | yes | `0` ok · `2` error |
| `check` | Verify the on-disk `public-api.ts` matches what would be generated. Use in CI to block drift. | no | `0` match · `1` drift · `2` error |
| `diff` | Print a unified diff between expected and actual output. | no | `0` match · `1` differ · `2` error |
| `docs` | Document every library's public API in a workspace as Markdown. | yes | `0` ok · `2` error |
| `inventory` | Inventory every developer-authored object (apps + libraries) as Markdown. | yes | `0` ok · `2` error |
| `ficd-init` | Seed a starter `ficd/` metadata folder into each library (run before `ficd`). | yes | `0` ok · `2` error |
| `ficd` | Generate a Functional Interface Control Document per library from authored `ficd/` metadata. | yes | `0` ok · `2` error |
| `providers` | Generate an Angular `provide-<project>.ts` wiring interfaces, tokens, and implementations. | yes | `0` ok · `2` error |

### Options

- `--project <path>` — path to the project directory *or* directly to `ng-package.json`. If omitted, SurfaceQ searches upward from the current directory. For `docs`, `inventory`, and `ficd`, this is the **workspace root** to search for projects.
- `--verbosity <level>` — `quiet`, `minimal`, `normal` (default), `detailed`, `diagnostic`. `diagnostic` emits trace lines for the walker and sidecar.
- `--output <path>` *(docs / inventory / ficd)* — destination relative to each project directory. A Markdown file for `docs` (`API.md`) and `inventory` (`INVENTORY.md`); a directory for `ficd` (`docs/ficd`).
- `--include-implementations` *(docs only)* — include classes that implement an exported interface. Hidden by default (see below).

## Documenting a workspace

`surfaceq docs` walks a workspace, finds every library (`ng-package.json`, skipping `node_modules` and `dist`), and writes a Markdown reference next to each one:

```sh
surfaceq docs --project ./my-workspace
# writes libs/auth/API.md, libs/data/API.md, …
```

Each document is titled from the library's `package.json` `name` (falling back to the directory name) and contains tables for:

- **Interfaces** — each property (type, optional, readonly) and method (parameters and return type).
- **Injection Tokens** — the token and the **contract type** `T` from `new InjectionToken<T>(…)`, so consumers depend on the interface rather than an implementation.
- **Enums** — members and their (declared or computed) values.
- **Type Aliases** — the aliased definition.
- **Classes** and **Functions** — public members and signatures.

JSDoc summaries become the Description column. Output is deterministic (declarations sorted by name) and table-safe (pipes in union types are escaped). Use `--output docs/api.md` to change the per-library destination.

### Hiding implementations

The document is meant to show the **contract** a consumer codes against — exported interfaces and injection tokens — not the concrete classes behind them. So by default, a class that **implements an interface exported by the same library** is omitted: consumers should inject its token and depend on the interface, not the class.

```sh
surfaceq docs --project ./my-workspace               # BillsService (implements IBillsService) is hidden
surfaceq docs --project ./my-workspace --include-implementations   # show it anyway
```

Classes that implement only an external interface (e.g. Angular's `ControlValueAccessor`) or no interface at all are used directly and remain in the document. Hidden classes are reported on stdout so the omission is never silent.

### Marking things deprecated

Tag any declaration or member with the standard TypeScript `@deprecated` JSDoc tag — the same tag IDEs and the language service already understand. No config, no new syntax. The optional text after the tag becomes the reason.

```ts
/** @deprecated Use {@link Bill.partnerShare} instead; removed in v3. */
export interface LegacyBill {
  /** @deprecated since v2 — use `amount`. */
  readonly total: number;
}
```

In the generated `API.md`:

- Every table gains a **`Deprecated`** column showing the reason (or `yes` when no reason is given, `no` otherwise).
- A deprecated interface, class, or enum also gets a `> ⚠️ **Deprecated** — …` callout under its heading.
- When anything is deprecated, a **`Deprecations`** summary table is listed at the top of the document as an at-a-glance index.

## Inventorying a workspace

`docs` describes the *public contract* of each library. `surfaceq inventory`
answers a different question: **what code actually exists here?** It produces a
complete census of every developer-authored object — exported *and* internal —
across an Angular application, library, or whole workspace, and writes one
`INVENTORY.md` next to each project.

```sh
surfaceq inventory --project ./my-workspace
# writes apps/web/INVENTORY.md, libs/auth/INVENTORY.md, …
```

How it differs from `docs`:

- **Apps too, not just libraries.** Projects are discovered from `angular.json`
  (Angular CLI), `project.json` (Nx), and `ng-package.json` (libraries). Point
  `--project` at a workspace, a single app, or a single library.
- **Everything, not just the public API.** Every top-level declaration is
  listed and flagged `Exported: yes/no` — internal helpers included.
- **Angular-aware.** Each object is grouped by its role — **Component,
  Directive, Pipe, Service, NgModule, Guard, Resolver, Interceptor** — detected
  from decorators, implemented interfaces, and functional types
  (`CanActivateFn`, `ResolveFn`, `HttpInterceptorFn`), with plain TypeScript
  kinds (Class, Interface, Enum, Type Alias, Function, Constant, Injection
  Token) for the rest.

Each `INVENTORY.md` opens with a `Summary` (total / exported / internal counts
and a per-category table), then one section per non-empty category:

```md
## Components

| Name | Kind | Exported | File | Description |
| --- | --- | --- | --- | --- |
| `AppComponent` | `class` | yes | `src/app/app.component.ts` | The shell. |
```

Tests are excluded (`*.spec.ts`, `*.stories.ts`, `*.e2e-spec.ts`, `*.d.ts`, and
`*-e2e` projects), as are `node_modules` and `dist`. Output is deterministic
(sorted by name then file) and table-safe, just like `docs`. Use
`--output reports/INVENTORY.md` to change the per-project destination.

## Generating a Functional Interface Control Document

`docs` auto-generates a flat API reference. `surfaceq ficd` produces something a
spec reviewer recognizes: a **multi-file Functional Interface Control Document**
— numbered sections, identity blocks, capability groupings, member-by-member
tables, requirements ("shall" statements) and cross-references — whose *structure
and prose are authored by you* and whose *tables are filled from the code*.

You drive it with a `ficd/` folder at the library root. The command merges that
authored metadata with the extracted public API and writes a document set
(default `docs/ficd/`, one rendered `*.md` per authored file plus a `README.md`
index). The workflow is **seed → edit → generate**:

```sh
surfaceq ficd-init --project ./my-workspace   # 1. seed a starter ficd/ in each library
#                                               2. open the seeded files and fill the TODOs
surfaceq ficd --project ./my-workspace        # 3. generate the document set
# libs/auth/ficd/…           ← you author this (seeded by ficd-init; see docs/specs/ficd-schema.md)
# libs/auth/docs/ficd/…      ← SurfaceQ generates this
```

`ficd-init` writes a `ficd.yml` manifest plus placeholder section files with
`TODO` markers, commented-out `groups:` examples, and `<!-- add services here -->`
guidance. It never overwrites an existing file, so it is safe to re-run.

A section file is YAML-style frontmatter plus a Markdown body. Frontmatter binds
authored prose to symbol names the renderer resolves from the code:

```md
---
template: services
section: 8
title: Core API — Services
inventory: true
groups:
  - title: Outbound messaging — Command / Request / Query
    heading: 8.3
    access: Action
    interfaces: [ICommandService, IRequestService, IQueryService]
    tokens: [COMMAND_SERVICE, REQUEST_SERVICE, QUERY_SERVICE]
    classes: [CommandService, RequestService, QueryService]
requirements:
  - A plugin **shall** inject the `InjectionToken`, not the concrete class.
---
Idiomatic injection from a tile is shown below.
```

Templates are `narrative` (default), `services` (member tables + an optional
service-inventory table), `functions` (signature/parameter tables), and
`data-objects` (field tables + reconstructed TypeScript + value-set tables). A
library with no `ficd/` folder is skipped; the input and output trees mirror, so
relative cross-links survive. Only authored `date` values appear — never the
system clock — so output stays byte-identical across runs. The full schema is in
[`docs/specs/ficd-schema.md`](docs/specs/ficd-schema.md).

## Generating Angular providers

`surfaceq providers` writes a `provide-<project>.ts` for each library, wiring its
dependency-injection surface from the code itself. For every
`InjectionToken<T>` it finds:

- if `T` is an interface implemented by an exported class, it binds the token to
  that class with `useExisting`;
- otherwise `T` is treated as a configuration value and bound with `useValue`
  from a generated `Provide<Project>Options` field.

```sh
surfaceq providers --project ./my-workspace
# writes libs/api/src/lib/provide-api.ts, …
```

Given a contract (`IBillsService` + `BILLS_SERVICE`), an implementation
(`BillsService implements IBillsService`, `providedIn: 'root'`), and a config
token (`API_BASE_URL: InjectionToken<string>`), it produces:

```typescript
// This file is generated by SurfaceQ. Do not edit by hand.
// Regenerate with: surfaceq providers

import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { API_BASE_URL } from './api-base-url.token';
import { BILLS_SERVICE } from './services/bills.service.contract';
import { BillsService } from './services/bills.service';

export interface ProvideApiOptions {
  readonly apiBaseUrl: string;
}

export function provideApi(options: ProvideApiOptions): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: API_BASE_URL, useValue: options.apiBaseUrl },
    { provide: BILLS_SERVICE, useExisting: BillsService },
  ]);
}
```

The file is placed at the common-ancestor directory of the wired declarations
(so relative imports match your source layout) and overwritten in place. A token
whose interface has no implementation is reported as a warning and skipped; a
library with no injectable contracts is skipped entirely. Output is deterministic
and LF/UTF-8. Because the generated file lives under the scan root, a subsequent
`generate` run includes `provideApi` in `public-api.ts` automatically. (v1
includes every implemented contract; a future revision will honor `@internal` /
`@publicApi` JSDoc tags.)

## Manifest

SurfaceQ reads only one field from `ng-package.json`:

```json
{
  "entryFile": "src/public-api.ts"
}
```

If `entryFile` is omitted, SurfaceQ defaults to `src/public-api.ts` and logs an info message.

## What gets exported

SurfaceQ discovers the following declarations via the TypeScript compiler API (delivered by a bundled sidecar):

- `export class`, `export interface`, `export type`, `export enum`, `export const enum`
- `export const`, `export let`, `export var`, `export function`
- `export const TOKEN = new InjectionToken<…>('…')`
- `export { X } from './…'` and `export type { X } from './…'`
- `export * from './…'` — expanded into the declaring file's explicit re-exports

`export default …` is intentionally skipped and reported as a `default-export-skipped` warning. Files named `index.ts`, `*.spec.ts`, `*.stories.ts`, the entry file itself, and anything under `node_modules/` are excluded from the scan.

## Output shape

```ts
// ============================================================
// SurfaceQ — generated public API. DO NOT EDIT.
// Regenerate with `surfaceq generate`.
// ============================================================
export { A, B } from './lib/a';
export type { Shape } from './lib/shape';
```

- One `export { … } from './…';` line per declaring file, values first.
- One `export type { … } from './…';` line per file that contributes type-only symbols.
- POSIX forward slashes, no `.ts` extension, relative to the entry file's directory, always prefixed with `./` or `../`.
- Two-space indentation. LF line endings. Exactly one trailing newline.

## CI integration

`check` is the command you want in CI. It produces a concise one-line message on drift and exits non-zero, which pairs cleanly with GitHub Actions, Azure Pipelines, or any other runner:

```yaml
- run: surfaceq check --project libs/my-lib
```

Use `diff` locally when you want to see what changed.

## Architecture

```
+-----------------+        +-----------------------+
|  .NET CLI       | stdin  |  Node sidecar         |
|  (surfaceq.exe) | -----> |  sidecar.js           |
|                 | <----- |  (TypeScript Compiler)|
+-----------------+ stdout +-----------------------+
        |
        v
   public-api.ts
```

The .NET host walks the file system, invokes a single long-lived Node process via line-delimited JSON-RPC (`ping` / `discover` / `document` / `inventory` methods), and renders the result. The sidecar owns the TypeScript compiler API; the host owns file I/O, grouping, and ordering. This split keeps the CLI testable without spinning up Node and keeps the TypeScript dependency out of .NET.

## Build from source

Requires .NET 8 SDK and Node.js 22.

```sh
git clone https://github.com/QuinntyneBrown/SurfaceQ.git
cd SurfaceQ
npm ci --prefix src/SurfaceQ.Sidecar.Node
dotnet build
dotnet test
```

To pack the global tool locally:

```sh
dotnet pack src/SurfaceQ.Cli -c Release -o ./artifacts
dotnet tool install --global --add-source ./artifacts SurfaceQ
```

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](./CONTRIBUTING.md) for the development loop (ATDD + vertical slices), commit style, and PR checklist. All contributors are expected to follow our [Code of Conduct](./CODE_OF_CONDUCT.md).

## Security

To report a security vulnerability, please follow the process in [SECURITY.md](./SECURITY.md). Do not open public issues for security reports.

## License

SurfaceQ is released under the [MIT License](./LICENSE).
