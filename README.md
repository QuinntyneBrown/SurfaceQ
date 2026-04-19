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

### Options

- `--project <path>` — path to the project directory *or* directly to `ng-package.json`. If omitted, SurfaceQ searches upward from the current directory.
- `--verbosity <level>` — `quiet`, `minimal`, `normal` (default), `detailed`, `diagnostic`. `diagnostic` emits trace lines for the walker and sidecar.

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

The .NET host walks the file system, invokes a single long-lived Node process via line-delimited JSON-RPC (`ping` / `discover` methods), and renders the result. The sidecar owns the TypeScript compiler API; the host owns file I/O, grouping, and ordering. This split keeps the CLI testable without spinning up Node and keeps the TypeScript dependency out of .NET.

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
