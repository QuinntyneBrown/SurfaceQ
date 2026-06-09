# Investigation: Can Compodoc replace SurfaceQ's documentation extraction?

**Date:** 2026-06-09
**Question asked:** *Can the open-source documentation tool Compodoc produce the same
documentation from Angular code that SurfaceQ does? Does it output JSON from Angular code?*

## TL;DR

- **Yes, Compodoc reads Angular/TypeScript source and produces documentation** of components,
  directives, pipes, modules, injectables, interfaces, classes, enums, type aliases, functions,
  variables, routes, and guards — a *superset* of the declaration kinds SurfaceQ's `docs`/`inventory`
  commands surface.
- **Yes, Compodoc can output JSON.** Passing `-e json` / `--exportFormat json` writes a single
  `documentation.json` to the output folder instead of the HTML site. That file is the parsed model
  of the whole project (the same data Storybook consumes via `setCompodocJson`).
- **But it is not a drop-in replacement for SurfaceQ.** Compodoc and SurfaceQ answer different
  questions and have incompatible design constraints. Compodoc is an *application-documentation
  website generator*; SurfaceQ is a *deterministic, build-gated API-contract generator* for Angular
  *libraries*. The gaps below (determinism, public-API contract scoping, `public-api.ts` generation,
  DI-provider generation, the FICD authoring workflow, byte-identical output, no-network/no-clock
  guarantees) are exactly where SurfaceQ exists. Compodoc could plausibly be used as an **alternative
  TypeScript-parsing backend** for the *docs/inventory* slice, but adopting it would forfeit the
  contracts SurfaceQ is built around.

---

## 1. What Compodoc is

Compodoc ([compodoc.app](https://compodoc.app/), npm [`@compodoc/compodoc`](https://www.npmjs.com/package/compodoc))
is "the missing documentation tool for your Angular application." It parses a project via the
TypeScript compiler API (driven from a `tsconfig`) and, by default, generates a **static HTML
documentation website** with search (lunr.js), navigation, themes, and diagrams.

It is fundamentally an Angular *application* documentation generator with a rich UI, not a
machine-readable contract tool — though its `-e json` mode does expose the parsed model.

### What it documents

Per the official docs and the Storybook integration, Compodoc extracts and groups by Angular role:

| Compodoc category | Contents |
|---|---|
| Modules | `@NgModule`s, their declarations/imports/exports/providers, module dependency graph |
| Components | `@Component`s with `@Input`/`@Output`, properties, methods, lifecycle hooks, the DOM tree and template tab |
| Directives | `@Directive`s |
| Pipes | `@Pipe`s |
| Injectables | `@Injectable` services |
| Guards / Interceptors | Router guards, HTTP interceptors |
| Interfaces | members + methods |
| Classes | "classical" classes (non-decorated) |
| Enums | enum members |
| Miscellaneous | functions, variables, type aliases, enumerations not attached to a class |
| Routes | route table + routes graph |
| Coverage | documentation-coverage report (% of symbols with JSDoc) |

JSDoc support includes `@param`, `@returns`, `@link`, `@ignore`, and `@example`.

### Key visual/UX features (HTML mode)

- Module dependency, component hierarchy, and DI graphs; routes graph.
- Documentation **coverage report** with a configurable threshold (`--coverageTest`,
  `--coverageMinimumPerFile`, `--coverageTestThresholdFail`) — useful as a CI gate.
- Seven+ themes (gitbook, material, readthedocs, stripe, vagrant, laravel, postmark, original),
  dark mode, mobile-friendly, lunr.js search.

---

## 2. Does Compodoc output JSON?

**Yes.** JSON export was requested in
[issue #196](https://github.com/compodoc/compodoc/issues/196) (2017, marked *Completed*) and is a
first-class option.

- CLI flag: **`-e json`** / **`--exportFormat json`** (the `exportFormat` schema property accepts
  `"json"` or `"html"`).
- Output: a single **`documentation.json`** written to the output folder (default `documentation/`,
  overridable with `--output`).
- Companion flag: **`--disableSourceCode`** keeps the embedded raw source out of the JSON
  (there is an explicit
  [commit `171862d`](https://github.com/compodoc/compodoc/commit/171862d) wiring `disableSourceCode`
  into the JSON export path), which substantially shrinks and stabilizes the file.
- `--watch` can be combined with `-e json` to regenerate on change
  ([issue #862](https://github.com/compodoc/compodoc/issues/862)).

### Shape of `documentation.json`

It is the full parsed model of the project — top-level keys mirror the categories above:
`components`, `directives`, `injectables`, `interfaces`, `classes`, `pipes`, `guards`,
`interceptors`, `modules`, `miscellaneous` (with `variables`, `functions`, `typealiases`,
`enumerations`), `routes`, `coverage`, plus `package` metadata. This is exactly the artifact
Storybook ingests:

```ts
import compodocJson from '../documentation.json';
import { setCompodocJson } from '@storybook/addon-docs/angular';
setCompodocJson(compodocJson);
```

### Caveats on the JSON

- **It is not guaranteed deterministic.** [Issue #981](https://github.com/compodoc/compodoc/issues/981)
  reports Compodoc producing *different* `documentation.json` across runs with **no source changes**
  (e.g., when two symbols share a name like `environment.ts` / `environment.prod.ts`). This directly
  violates SurfaceQ's byte-identical-output contract.
- **Field coverage has had gaps** — e.g. [issue #909](https://github.com/compodoc/compodoc/issues/909):
  `rawdescription` missing on several types in JSON output.
- The JSON is a *rendering model*, not a curated public-API contract: there is no notion of "the
  declarations reachable from `ng-package.json`'s entry file" or "values vs `export type`."

---

## 3. Side-by-side: Compodoc vs SurfaceQ

| Dimension | Compodoc | SurfaceQ |
|---|---|---|
| Primary purpose | Browsable docs **website** for an Angular **app** | Deterministic **API artifacts** for Angular **libraries** |
| Parsing backend | TypeScript compiler API (in-process, Node) | TS compiler API in a **Node sidecar** driven by a **.NET host** |
| Input scoping | Whole project via `tsconfig` | Files under the `ng-package.json` `entryFile` scan root |
| Output formats | HTML site (default) **or** `documentation.json` | `public-api.ts`, Markdown (`API.md`/`SERVICE_API.md`/`INVENTORY.md`), FICD doc set, `provide-*.ts` |
| Determinism | **Not guaranteed** (issue #981) | **Byte-identical** across OS/runs — a hard contract |
| Coverage gate | Built-in (`--coverageTest`) | Not its job (drift is gated by `check`/`diff` exit codes) |
| Public-API contract | No concept of it | Core concept: re-export surface, value vs `export type` |
| Generates `public-api.ts` | No | Yes (`generate`) |
| Drift checking in CI | No (coverage only) | Yes — `check` (exit 1 on drift), `diff` |
| Implementation hiding | `--disablePrivate`/`--disableInternal` (member-level) | Hides classes reached via injection tokens; `@deprecated` excluded by default |
| Angular DI wiring generation | No | Yes — `providers` emits `provide-<project>.ts` |
| Authored-doc merge workflow | `includes` (external markdown pages) | FICD: structured `ficd/` authoring + extracted API merge |
| Network / clock | Can fetch (Google Analytics opts), uses time | **No network, no system clock** (author-supplied dates) |
| Host runtime | Node only | .NET 8 global tool wrapping a Node sidecar |
| Markdown table output | No (HTML/JSON) | Yes — Markdown is a first-class output |

### Where they overlap (Compodoc *could* do this)

- The **`docs`** command's job (enumerate exported declarations, members, JSDoc, enum members,
  type aliases, `InjectionToken<T>` contract types) is largely covered by Compodoc's parsed model.
- The **`inventory`** command's "census of every developer-authored object, classified by Angular
  role" maps almost directly onto Compodoc's role buckets (Component/Directive/Pipe/Service/
  Module/Guard/Interceptor) and its inclusion of non-exported declarations.

### Where Compodoc cannot substitute for SurfaceQ

1. **`generate`/`check`/`diff`** — Compodoc has no concept of a generated `public-api.ts` barrel,
   value-vs-type re-export separation, or drift-checking exit codes. This is SurfaceQ's reason to exist.
2. **`providers`** — generating `provide-<project>.ts` DI wiring (`useExisting` vs `useValue` +
   options) from `InjectionToken<T>` analysis has no Compodoc analogue.
3. **`ficd` / `ficd-init`** — the structured authored-metadata-plus-extracted-API merge (manifest,
   sections, groups, frontmatter, numbered spec-style output) is bespoke; Compodoc only supports
   pasting external markdown pages.
4. **Determinism guarantees** — SurfaceQ's byte-identical, LF/UTF-8, ordinal-sorted, no-clock,
   no-network contracts are explicitly required by its CI and are *not* offered (and demonstrably
   violated) by Compodoc.
5. **Libraries-first scoping** — SurfaceQ's `docs` traces the public contract per `ng-package.json`
   library; Compodoc documents the whole app/project from a `tsconfig` with no library partitioning.

---

## 4. Could SurfaceQ use Compodoc internally?

Two narrow possibilities, both with significant friction:

- **As an alternative parser for `docs`/`inventory` only.** SurfaceQ could shell out to
  `compodoc -e json --disableSourceCode` and consume `documentation.json` instead of its own
  `document`/`inventory` sidecar RPC methods. The Angular-role classification and member extraction
  would come "for free." **Costs:** (a) determinism is not guaranteed (issue #981) — SurfaceQ would
  have to post-process and re-sort to restore byte-identical output, and validate every release;
  (b) it adds a heavy Node dependency (`@compodoc/compodoc` + its transitive tree) that SurfaceQ
  currently avoids by vendoring only `typescript`; (c) it covers only ~2 of the 8 commands, so the
  custom sidecar stays regardless. Net: **not worth it** — the existing sidecar is lighter,
  deterministic, and already shared across all pipelines.

- **As a complementary, separately-invoked tool.** Teams can run Compodoc *alongside* SurfaceQ for
  the rich browsable site + coverage gate, while SurfaceQ owns the deterministic contract artifacts.
  This is the realistic relationship: **complementary, not competitive.**

---

## 5. Conclusion

- Compodoc **can** read Angular code and document the same declaration kinds SurfaceQ surfaces (and
  more), and it **does** emit JSON via `-e json` → `documentation.json`.
- It is **not** a replacement for SurfaceQ: it lacks public-API-barrel generation, drift checking,
  DI-provider generation, the FICD authoring workflow, deterministic byte-identical output, and the
  no-network/no-clock guarantees that define SurfaceQ. Its JSON is also documented to be
  non-deterministic across runs.
- **Recommendation:** treat Compodoc as a **complementary** documentation *website* + coverage tool,
  not as the engine behind SurfaceQ's contract artifacts. If a future SurfaceQ feature only needs a
  parsed model for a browsable site, Compodoc's `documentation.json` is a reasonable input — but the
  existing custom sidecar should remain the source of truth for everything determinism-critical.

---

## Sources

- [Compodoc — official site](https://compodoc.app/)
- [Compodoc options/usage guide](https://compodoc.app/guides/usage.html)
- [`compodoc` on npm](https://www.npmjs.com/package/compodoc)
- [Compodoc config `schema.json` (repo)](https://github.com/compodoc/compodoc/blob/develop/package.json) — `exportFormat`, `disableSourceCode`, coverage, visibility options
- [Issue #196 — JSON export support (Completed)](https://github.com/compodoc/compodoc/issues/196)
- [Issue #862 — `--watch` with `--exportFormat json`](https://github.com/compodoc/compodoc/issues/862)
- [Commit 171862d — `disableSourceCode` flag for json format](https://github.com/compodoc/compodoc/commit/171862d)
- [Issue #981 — non-deterministic `documentation.json` across runs](https://github.com/compodoc/compodoc/issues/981)
- [Issue #909 — JSON output missing `rawdescription`](https://github.com/compodoc/compodoc/issues/909)
- [Storybook Angular + Compodoc (`setCompodocJson`)](https://storybook.js.org/docs/angular/writing-docs/autodocs)
- [Vojtech Ruzicka — Documenting Angular apps: TypeDoc, Compodoc, AngularDoc](https://www.vojtechruzicka.com/documenting-angular-apps-with-typedoc-compodoc-and-angulardoc/)
