namespace SurfaceQ.Core;

// The model for the `surfaceq ficd` command: a Functional Interface Control
// Document assembled from user-authored Markdown metadata (a `ficd/` folder)
// merged with the extracted public API (a LibraryApi). The authored side drives
// identity, sectioning, prose, groupings, requirements and cross-references; the
// extracted side fills the member/field/parameter tables. See docs/specs/
// ficd-schema.md for the authoring schema this models.

// Global document identity from `ficd/ficd.yml`, plus the ordered list of section
// document paths used for the generated README index. Identity fields are empty
// when the manifest omits them; an empty field is omitted from the identity block.
public sealed record FicdManifest(
    string DocumentTitle,
    string DocumentNumber,
    string Version,
    string Date,
    string Interface,
    string Package,
    string Status,
    string ChangeAuthority,
    IReadOnlyList<string> Documents);

// One capability group inside a section: authored prose bound to symbol names
// that the renderer resolves against the extracted API. Symbol-name lists keep
// their authored order so rendered tables are deterministic.
public sealed record FicdGroup(
    string Title,
    string Heading,
    string Intro,
    string Access,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> Tokens,
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> Functions,
    IReadOnlyList<string> Types,
    IReadOnlyList<string> DataObjects);

// One authored section file. RelativePath is POSIX, relative to the `ficd/`
// folder, and is reused verbatim as the output path so the input and output
// trees mirror (and relative cross-links survive). Template selects the
// auto-generation applied on top of the authored body.
public sealed record FicdSection(
    string RelativePath,
    string Template,
    int Section,
    string Title,
    string Scope,
    string Summary,
    bool Inventory,
    IReadOnlyList<FicdGroup> Groups,
    IReadOnlyList<string> Requirements,
    IReadOnlyList<string> CrossReferences,
    string Body);

// Everything read from one library's `ficd/` folder, ready to render alongside
// its extracted LibraryApi.
public sealed record FicdProject(
    string LibraryName,
    FicdManifest Manifest,
    IReadOnlyList<FicdSection> Sections);

// One rendered output file. RelativePath is POSIX, relative to the output root.
public sealed record FicdOutputFile(string RelativePath, string Content);
