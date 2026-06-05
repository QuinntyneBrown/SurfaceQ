# FICD Authoring Schema

This document defines the **user-authored Markdown metadata** that drives the
`surfaceq ficd` command. The command produces a multi-file **Functional Interface
Control Document** (FICD) for an Angular library by merging two inputs:

1. **Extracted API facts** — interfaces, injection tokens, classes, functions,
   type aliases, enums and constants discovered from the library's TypeScript
   sources (the same `document` extraction the `docs` command uses).
2. **Authored narrative and structure** — a `ficd/` folder of Markdown files,
   defined by this schema, that supplies document identity, sectioning, overview
   prose, headings, capability groupings, requirements ("shall" statements) and
   cross-references.

The host owns grouping, ordering and rendering; the sidecar owns parsing. The
output is byte-identical across runs and hosts, LF-terminated, UTF-8 without BOM
— the same determinism contract every SurfaceQ command honors.

---

## 1. Where the metadata lives

Author the metadata in a `ficd/` folder at the **library root** (next to
`ng-package.json`). Run **`surfaceq ficd-init`** to seed this folder with a
`ficd.yml` manifest and placeholder section files (with `TODO` markers,
commented-out `groups:` examples, and `<!-- add … here -->` guidance); fill in
the placeholders, then run `surfaceq ficd`. The generated document set is written
to a separate output folder (default `docs/ficd/`). A library with no `ficd/` folder is skipped; the
command requires at least one library with a `ficd/` folder.

> **Why not `FICD/`?** On a case-insensitive filesystem (Windows, default macOS)
> an output folder named `FICD/` would resolve to the same directory as the input
> `ficd/`, and generation would overwrite the author's metadata. The default
> output is therefore `docs/ficd/`, and the command refuses to write when the
> resolved output directory is the `ficd/` input folder.

```
libs/auth/
  ng-package.json
  package.json
  src/…                         ← TypeScript sources (extracted)
  ficd/                         ← authored metadata (INPUT, this schema)
    ficd.yml
    introduction.md
    applicable-and-reference-documents.md
    detailed-interface-definition/
      overview.md
      plugin-mdf-interface/     ← any slug; matches interfaceSlug in ficd.yml
        discovery-and-registration.md
        lifecycle-hooks.md
        core-api/
          callbacks.md
          functions.md
          services.md
        data-object-definitions.md
        constraints.md
        exception-policy.md
        appendices/
          definitions-and-acronyms.md
          identified-tbds-and-tbcs.md
          checklist-trace-matrix.md
  docs/ficd/                    ← generated document set (OUTPUT, default)
    README.md                   ← generated from ficd.yml
    introduction.md
    detailed-interface-definition/…   (mirrors the ficd/ tree)
```

**The input tree mirrors the output tree.** Every `*.md` under `ficd/` (except
`ficd.yml`) becomes one rendered `*.md` at the *same relative path* under the
output folder. Because the trees mirror, relative cross-links an author writes in
prose (`./functions.md`, `../constraints.md`) resolve correctly in the output
with no rewriting. `<output>/README.md` (default `docs/ficd/README.md`) is
generated from the manifest; an authored `ficd/README.md` is **ignored** (the
README is always generated, so it is never read back as a section).

---

## 2. The frontmatter dialect

Section files carry **YAML-style frontmatter** delimited by a leading `---`
fence; `ficd.yml` is frontmatter with no fence (the whole file). To stay
dependency-free, SurfaceQ parses a **documented, constrained subset** of YAML —
not arbitrary YAML. The supported forms are:

| Form | Syntax | Example |
| --- | --- | --- |
| Scalar | `key: value` | `section: 8` |
| Quoted scalar | `key: "value"` (quotes stripped) | `package: "@gers/mdf (Angular 17)"` |
| Block list | `key:` then indented `- item` lines | see `requirements` below |
| Inline list | `key: [a, b, c]` | `interfaces: [ICommandService, IRequestService]` |
| List of maps | **only** the `groups:` key (see §4) | see §4 |
| Comment | a line whose first non-space char is `#` | `# this is ignored` |

Rules:

- Keys are `lowerCamelCase`, letters/digits/`-` only, at column 0 (top level).
- A scalar value is the remainder of the line, trimmed; surrounding `"`/`'` are
  stripped. A value is never multi-line — use the **body** for prose.
- Inline-list items are split on commas and trimmed; surrounding quotes stripped.
- `groups:` is the **only** list-of-maps key. Every other block/inline list is a
  list of scalars.
- Unknown keys are ignored (forward-compatible). Absent keys take their default.

Anything that does not fit this subset belongs in the **body** (the Markdown
after the closing `---`), which is copied through verbatim.

---

## 3. The manifest — `ficd/ficd.yml`

Global document identity plus the ordered document list for the README index.

```yaml
documentTitle: Functional Interface Control Document — MDF ↔ Plugin Interface
documentNumber: GERS-MDF-FICD
version: 1.0 (Draft)
date: 2026-06-04
interface: Frontend Plugin ↔ Main Dashboard Framework (MDF)
package: "@gers/mdf (Angular 17)"
status: Draft for review
changeAuthority: GERS Documentation Maintainers
interfaceSlug: plugin-mdf-interface
documents:
  - introduction.md
  - applicable-and-reference-documents.md
  - detailed-interface-definition/overview.md
  - detailed-interface-definition/plugin-mdf-interface/core-api/callbacks.md
  - detailed-interface-definition/plugin-mdf-interface/core-api/functions.md
  - detailed-interface-definition/plugin-mdf-interface/core-api/services.md
  - detailed-interface-definition/plugin-mdf-interface/data-object-definitions.md
  - detailed-interface-definition/plugin-mdf-interface/constraints.md
  - detailed-interface-definition/plugin-mdf-interface/exception-policy.md
```

| Field | Required | Drives |
| --- | --- | --- |
| `documentTitle` | recommended | README H1 and the **Document Title** row of every identity block. |
| `documentNumber` | optional | **Document Number** identity row. |
| `version` | optional | **Version** identity row. |
| `date` | optional | **Date** identity row. Author-supplied (never `now()` — determinism). |
| `interface` | optional | **Interface** identity row. |
| `package` | optional | **Package** identity row. |
| `status` | optional | **Status** identity row. |
| `changeAuthority` | optional | **Change Authority** identity row. |
| `interfaceSlug` | optional | Documented only; the slug folder is whatever the author names it. |
| `documents` | recommended | Ordered list of relative paths for the README document-set table. Paths not listed are still rendered, but appended (sorted) after the listed ones. |

Identity rows whose value is empty are omitted from the block.

If `ficd.yml` is absent, the command still renders every section file using empty
identity values, and the README lists all section files sorted by path.

---

## 4. Section files — `ficd/**/*.md`

Each section file is `--- frontmatter ---` followed by an optional Markdown body.

### 4.1 Common frontmatter (every template)

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `template` | scalar | `narrative` | One of `narrative`, `services`, `functions`, `data-objects`. Selects auto-generation (§5). |
| `section` | scalar (int) | _none_ | Section number used to auto-number group headings (`{section}.{n}`). |
| `title` | scalar | file stem | H1 of the document and the **Section** identity row. |
| `scope` | scalar | _empty_ | One-line scope used in the README document-set table. |
| `summary` | scalar | _empty_ | Lead paragraph rendered under the identity block. |
| `inventory` | scalar (bool) | `false` | For `services`/`functions`: render an inventory table built from the groups (§5). |
| `requirements` | block/inline list | _empty_ | "shall" statements; rendered as a numbered **Requirements** list. |
| `crossReferences` | block/inline list | _empty_ | Rendered as a trailing **Cross-references** bullet list (Markdown links allowed). |
| `groups` | list of maps | _empty_ | Capability groups binding prose to extracted symbols (§4.2). |

### 4.2 The `groups` list of maps

`groups` is the only list-of-maps key. Each item binds a heading and prose to a
set of **symbol names** resolved from the extracted API. Item fields:

| Key | Type | Meaning |
| --- | --- | --- |
| `title` | scalar | Group heading text. |
| `heading` | scalar | Heading number (e.g. `8.3`). Defaults to `{section}.{index}`. |
| `intro` | scalar | One-line intro under the group heading (use the body for longer prose). |
| `access` | scalar | Free text for the inventory table's **Access** column (e.g. `Read`, `Action`). |
| `interfaces` | inline list | Interface symbol names to render (member tables / field tables). |
| `tokens` | inline list | Injection-token symbol names paired with the interfaces (by index). |
| `classes` | inline list | Concrete class names shown as the implementation (by index). |
| `functions` | inline list | Function symbol names (functions template). |
| `types` | inline list | Type-alias names (data-objects value sets). |
| `dataObjects` | inline list | Interface names rendered as data shapes (data-objects template). |

> Within a group, list fields use the **inline** `[a, b]` form (block `- item`
> lists are supported only at the top level of a section file). `interfaces`,
> `tokens` and `classes` are paired **by position**: the *i*-th interface pairs
> with the *i*-th token and the *i*-th class.

A name that does not resolve to an extracted symbol renders a `_(not found: X)_`
placeholder rather than failing — so a draft FICD can name symbols before the code
exists, and a rename is caught on the next run. A name that resolves to a symbol of
a *different* kind than the template expects (e.g. a class bound under `dataObjects`,
or a type alias bound under `interfaces`) renders that template's empty state
(`_No members._` / `_No fields._`) rather than erroring; bind names to the kind the
template documents.

```yaml
---
template: services
section: 8
title: Core API — Services
scope: All plugin-facing injectable services and stores (interface + token).
summary: This section specifies, member by member, every plugin-facing injectable service.
inventory: true
groups:
  - title: Outbound messaging — Command / Request / Query
    heading: 8.3
    access: Action
    intro: These three services emit messages to the backend through MDF's send pipeline.
    interfaces: [ICommandService, IRequestService, IQueryService]
    tokens: [COMMAND_SERVICE, REQUEST_SERVICE, QUERY_SERVICE]
    classes: [CommandService, RequestService, QueryService]
  - title: Streamed telemetry
    heading: 8.4
    access: Read
    interfaces: [IStreamedTelemetryStore]
    tokens: [STREAMED_TELEMETRY_STORE]
    classes: [StreamedTelemetryStore]
requirements:
  - A plugin **shall** inject the `InjectionToken`, not the concrete class.
  - A plugin **shall** type the injected reference against the `I…` interface.
crossReferences:
  - "[Callbacks](./callbacks.md) — reactive Signal model and release handles."
  - "[Functions](./functions.md) — provider functions."
---

Idiomatic injection from a tile:

​```typescript
const commandService: ICommandService = inject(COMMAND_SERVICE);
​```
```

---

## 5. Templates

Every template renders, in order: **identity block** → **summary** →
*template-specific auto-generated content* → **body** → **Requirements** →
**Cross-references**.

### 5.1 `narrative` (default)

No auto-generation. Identity block, summary, the authored body, requirements and
cross-references. Use for introduction, overview, constraints, exception policy,
discovery, lifecycle and appendices.

### 5.2 `services`

For each group:

- `## {heading} {title}` heading and the group `intro`.
- For each `interfaces[i]`, a heading `### \`{IName}\` / \`{TOKEN}\`` (the paired
  token is `tokens[i]` when present), an optional "Concrete class: `{class}`."
  line, the interface's JSDoc summary, then a **member table**:

  | Member | Kind | Signature | Description |
  | --- | --- | --- | --- |

  Properties render `name: type`; methods render `name(params): returnType`; the
  Kind column is `Property` / `Method`.

When `inventory: true`, a **Service inventory** table precedes the groups, one row
per interface: `Capability | Interface | Token | Concrete class | Access` (drawn
from each group's `title`, `interfaces`, `tokens`, `classes`, `access`).

### 5.3 `functions`

When `inventory: true`, a **Function inventory** table (`Function | Signature |
Description`) over all bound functions. Then, per group, the `intro` and a
function table (`Function | Parameters | Returns | Description`) for the group's
`functions`. With no groups, a single function table over the section-level
`functions` list (set it in a single unnamed group).

### 5.4 `data-objects`

For each group: a `## {heading} {title}` heading and the group `intro`, then its
data objects and value sets (so this template is `groups`-driven like `services`
and `functions`).

For each `dataObjects[i]` interface: `### \`{IName}\``, its JSDoc, a **fields
table** (`Field | Type | Required | Description`, where Required = not optional),
and a reconstructed TypeScript code block:

```typescript
interface IName {
  field: type;
  optional?: type;
}
```

For each `types[i]` type alias: a row in a **value-set** table
(`Type | Definition`) from the alias's definition text.

---

## 6. Output

For each library with a `ficd/` folder, the command writes the output tree
(default `docs/ficd/`, override with `--output <dir>` relative to the library
root; the output directory must not be the `ficd/` input folder):

- `<output>/README.md` — generated from `ficd.yml`: H1 (`documentTitle`, falling
  back to `"{library name} — FICD"` when empty), identity block, and a **Document
  Set** table (`# | Document | Scope`, from `documents` order and each section's
  `title`/`scope`).
- `<output>/<relative path>` — one per authored section file, identical relative path.

Determinism: section files are processed in `Ordinal` path order; symbols within a
table keep authored (group) order; the symbol lookup keeps the first declaration
on a name collision. Pipe characters in any table cell are escaped (`\|`); code
spans use a backtick fence one longer than the longest backtick run inside; an
**empty table cell** (missing token, class, scope, description, …) renders as an
en-dash `–`. A `documents` link target containing a space or parenthesis is wrapped
in angle brackets so the Markdown link is not truncated.

The output directory is validated up front: `--output` must be **relative** and
must **not** be the `ficd/` input folder or a subdirectory of it (so generated
files are never re-read as authored sections on a later run).

---

## 7. Exit codes

| Code | Meaning |
| --- | --- |
| `0` | At least one FICD was generated; all attempted libraries succeeded. |
| `2` | No library with a `ficd/` folder was found, a source parse error occurred, or an output file could not be written. |

---

## 8. Minimal worked example

Input:

```
libs/auth/ng-package.json          {"entryFile":"src/public-api.ts"}
libs/auth/package.json             {"name":"@acme/auth"}
libs/auth/src/public-api.ts        export * from './lib';
libs/auth/src/lib.ts               export interface AuthService { login(user: string): boolean; }
                                   export const AUTH = new InjectionToken<AuthService>('AUTH');
libs/auth/ficd/ficd.yml            documentTitle: Auth FICD
                                   documents: [detailed-interface-definition/core-api/services.md]
libs/auth/ficd/detailed-interface-definition/core-api/services.md
    ---
    template: services
    section: 8
    title: Core API — Services
    inventory: true
    groups:
      - title: Authentication
        heading: 8.1
        access: Action
        interfaces: [AuthService]
        tokens: [AUTH]
    ---
    The authentication surface.
```

Output `libs/auth/docs/ficd/detailed-interface-definition/core-api/services.md`
carries the identity block, a Service inventory table
(`| Authentication | \`AuthService\` | \`AUTH\` | – | Action |`), an
`### \`AuthService\` / \`AUTH\`` heading and a member table with the `login` method
(`login(user: string): boolean`), followed by the body prose.

`libs/auth/docs/ficd/README.md` lists the one document with its title and scope.
