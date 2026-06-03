# Plan: Deprecation support in `surfaceq docs`

**Status:** proposed
**Targets:** new requirement L2-030 (traces to L1-013)
**Suggested version:** minor bump to `0.11.0` (additive: a new column; default output gains a column)

## Goal

Let a library author mark a type, interface, enum, class, function, constant, injection token, or an individual member as **deprecated**, and have `surfaceq docs` surface that in the generated `API.md` — including a **Deprecated column** in the tables so consumers can see at a glance what to avoid.

## How an author marks something deprecated

Use the standard TypeScript **`@deprecated` JSDoc tag**, with an optional reason. No new syntax, no config file — this is what IDEs, the TypeScript language service, and `ng-packagr` already understand, and it keeps SurfaceQ's "no config" stance (L1-012).

```ts
/**
 * The amount owed.
 * @deprecated Use {@link Bill.partnerShare} instead; removed in v3.
 */
export interface LegacyBill {
  /** @deprecated since v2 — use `amount`. */
  readonly total: number;
}

/** @deprecated */
export type OldId = string;
```

- A declaration is deprecated if its leading JSDoc block contains a `@deprecated` tag.
- The **reason** is the free text after the tag (up to the next tag or end of block). It may be empty.
- The same applies to interface/class members and enum members.

## Where it surfaces in `API.md`

The document renders some kinds as **one table row per declaration** (Type Aliases, Functions, Constants, Injection Tokens) and others as **`###` headings with sub-tables** (Interfaces, Classes, Enums). Deprecation is shown in both shapes:

1. **Row-rendered declarations** — add a `Deprecated` column.

   ```md
   ## Type Aliases

   | Name | Definition | Deprecated | Description |
   | --- | --- | --- | --- |
   | `OldId` | `string` | yes | – |
   | `Id` | `string \| number` | no | An identifier. |
   ```

2. **Member tables** (interface/class Properties & Methods, Enum Members) — add a `Deprecated` column, so a single deprecated member stands out even when its parent is fine.

   ```md
   | Name | Type | Optional | Deprecated | Description |
   | --- | --- | --- | --- | --- |
   | `total` | `number` | no | since v2 — use `amount`. | – |
   ```

3. **Whole interface/class/enum deprecation** — these are `###` headings, not rows, so render a callout directly under the heading:

   ```md
   ### `LegacyBill`

   > ⚠️ **Deprecated** — Use Bill.partnerShare instead; removed in v3.
   ```

4. **(Recommended) "Deprecations" summary table** at the top of the document — a single column-based, at-a-glance index of everything deprecated in the library, only rendered when at least one item is deprecated:

   ```md
   ## Deprecations

   | Item | Kind | Reason |
   | --- | --- | --- |
   | `LegacyBill` | interface | Use Bill.partnerShare instead; removed in v3. |
   | `LegacyBill.total` | property | since v2 — use `amount`. |
   | `OldId` | type | – |
   ```

**Column value rule:** the `Deprecated` cell shows the reason text when present; `yes` when deprecated with no reason; `no` (or `–`) when not deprecated. Reason text is pipe-escaped and whitespace-collapsed like every other cell.

## Implementation by layer

Follows the existing host/sidecar split. Each layer mirrors how `doc` (the JSDoc summary) already flows through.

### 1. Sidecar — `src/SurfaceQ.Sidecar.Node/sidecar.js`

Today `getDoc` reads the leading `/** */` block and `cleanJsDoc` **stops at the first `@` tag** (line ~417), discarding all tags. We need to *capture* the `@deprecated` tag instead of only dropping it.

- Add `getDeprecation(node, sourceFile)` returning `{ deprecated: boolean, reason: string }`:
  - Reuse the `ts.getLeadingCommentRanges` + `/**`-prefix logic already in `getDoc`.
  - Walk the block's lines; when a line starts with `@deprecated`, set `deprecated = true` and collect the remainder of that line plus following lines until the next `@tag` as `reason` (then `collapse(...)`).
  - Prefer refactoring `getDoc`/`cleanJsDoc` into a single `parseJsDoc(text) -> { summary, tags }` pass so the block is scanned once and both the summary and `@deprecated` come from the same parse.
- Emit `deprecated` and `deprecationReason` on every object `describeDeclaration` and `describeMember` / `enumMembers` produce.

### 2. Model — `src/SurfaceQ.Core/ApiDoc.cs`

Add fields:
- `ApiDeclaration`: `bool Deprecated`, `string DeprecationReason`.
- `ApiMember`: `bool Deprecated`, `string DeprecationReason`.
- `EnumMember`: `bool Deprecated`, `string DeprecationReason`.

### 3. Pipeline — `src/SurfaceQ.Cli/DocumentationPipeline.cs`

Extend `ParseDeclaration` / `ParseMembers` / `ParseEnumMembers` to read the new JSON fields with the existing `Str`/`Bool` helpers. No other logic changes (deprecated items are still documented; the hide-implementations filter is orthogonal).

### 4. Renderer — `src/SurfaceQ.Core/MarkdownRenderer.cs`

- Add the `Deprecated` column to: `AppendPropertyTable`, `AppendMethodTable`, `AppendEnumBlock` member table, `AppendTypeAliasTable`, `AppendFunctionTable`, `AppendConstTable`, `AppendTokensTable`.
- Add a `AppendDeprecationCallout(sb, decl)` used by `AppendTypeBlock` (interfaces/classes) and `AppendEnumBlock` right after the heading.
- Add `AppendDeprecationsSummary(...)` rendered before `Contents` when any declaration or member is deprecated; list it in `Contents`.
- Introduce a small `DeprecatedCell(bool, string)` helper next to `Code`/`Cell` for consistent value rendering and pipe-escaping.
- Determinism is preserved: ordering is unchanged; the summary table follows the same name-ordering within kind.

## ATDD task breakdown (test-first vertical slices)

Each slice: write the failing acceptance test first (header `// Traces to: L2-030`), make it green, keep it small.

1. **Sidecar — declaration deprecation.** `@deprecated Reason` on a `type`/`interface`/`class`/`function`/`const`/token ⇒ `deprecated:true`, `deprecationReason:"Reason"`; absent ⇒ `false`,`""`. (`test/SurfaceQ.Integration.Tests/SidecarDocumentTests.cs`)
2. **Sidecar — member deprecation.** `@deprecated` on interface property/method and enum member ⇒ flagged. (same file)
3. **Model + pipeline.** Round-trip the new fields from sidecar JSON into `ApiDeclaration`/`ApiMember`/`EnumMember`.
4. **Renderer — columns.** Row tables and member tables include a `Deprecated` column with the reason/`yes`/`no` rule and pipe-escaping. (`test/SurfaceQ.Core.Tests/MarkdownRendererTests.cs`)
5. **Renderer — heading callout.** A deprecated interface/class/enum renders the `> ⚠️ **Deprecated** — …` callout under its heading.
6. **Renderer — summary table.** Present only when something is deprecated; omitted otherwise; listed in Contents.
7. **CLI end-to-end + determinism.** `docs` on a fixture with deprecations produces the expected `API.md`; two runs are byte-identical. (`test/SurfaceQ.Cli.Tests/DocsCommandTests.cs`)
8. **Docs + release.** Add L2-030, update `README.md` (docs section), `CLAUDE.md` (docs command note), bump `0.11.0`.

## New requirement to add (docs/specs/L2.md)

> **L2-030: Deprecation is surfaced** (Traces to: L1-013)
> A declaration or member carrying a `@deprecated` JSDoc tag shall be marked deprecated in the generated Markdown via a `Deprecated` column in its table (and a callout for heading-rendered interfaces/classes/enums); the tag's trailing text is shown as the reason.
> *Acceptance:* (1) `@deprecated Use X` on a type ⇒ its row's Deprecated column shows `Use X`. (2) a non-deprecated declaration shows `no`. (3) a deprecated interface renders a callout under its heading. (4) output remains deterministic.

## Edge cases

- **No reason** (`@deprecated` alone) ⇒ column shows `yes`, callout shows no trailing text.
- **Reason spanning multiple lines / containing `|` or `{@link …}`** ⇒ collapsed to one line and pipe-escaped; `{@link X}` left as-is (verbatim) for v1.
- **Deprecated member inside a non-deprecated parent** ⇒ only the member row is flagged.
- **`computed()` / un-typed members** ⇒ deprecation is independent of type inference; still flagged from JSDoc.
- **`@deprecated` on a re-export site** vs. the declaration — SurfaceQ documents declarations where they are declared, so the tag must be on the declaration (consistent with how `doc` already works).

## Out of scope (v1 of this feature)

- Failing/exiting non-zero on deprecated usage (that's a linter concern, not a doc generator).
- Resolving `{@link}` targets into Markdown links.
- A `--no-deprecated-column` toggle — the column is always present; revisit only if requested.
