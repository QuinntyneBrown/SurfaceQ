using System.Text;

namespace SurfaceQ.Core;

// Produces the starter `ficd/` metadata files for the `surfaceq ficd-init`
// command: a manifest plus placeholder section files with commented-out `groups`
// examples and `<!-- add … here -->` guidance. The author fills the TODOs and
// uncomments the groups, then runs `surfaceq ficd`. Output mirrors the FICD
// document tree (so a seeded scaffold renders without further edits) and uses LF
// endings regardless of how this source is checked out (content is joined with
// explicit "\n", never a raw string literal). See docs/specs/ficd-schema.md.

public sealed class FicdScaffolder
{
    public IReadOnlyList<FicdOutputFile> Scaffold(string libraryName)
    {
        var slug = Slugify(libraryName) + "-interface";
        var ifaceDir = "detailed-interface-definition/" + slug;
        return new List<FicdOutputFile>
        {
            new("ficd.yml", Manifest(libraryName, slug)),
            new("introduction.md", Introduction()),
            new("detailed-interface-definition/overview.md", Overview(slug)),
            new(ifaceDir + "/core-api/services.md", Services()),
            new(ifaceDir + "/core-api/functions.md", Functions()),
            new(ifaceDir + "/data-object-definitions.md", DataObjects()),
        };
    }

    private static string Manifest(string name, string slug) => Lines(
        "# FICD manifest — global document identity and the README document order.",
        "# Fill in the TODO values, then run `surfaceq ficd` to generate the document set.",
        $"documentTitle: TODO — Functional Interface Control Document for {name}",
        "documentNumber: TODO — e.g. ORG-LIB-FICD",
        "version: 0.1 (Draft)",
        "date: TODO — YYYY-MM-DD (author-supplied; never auto-dated)",
        $"interface: TODO — e.g. Consumer ↔ {name}",
        $"package: \"{name}\"",
        "status: Draft for review",
        "changeAuthority: TODO — owning team",
        $"interfaceSlug: {slug}",
        "# Order in which documents appear in the generated README index.",
        "documents:",
        "  - introduction.md",
        "  - detailed-interface-definition/overview.md",
        $"  - detailed-interface-definition/{slug}/core-api/services.md",
        $"  - detailed-interface-definition/{slug}/core-api/functions.md",
        $"  - detailed-interface-definition/{slug}/data-object-definitions.md");

    private static string Introduction() => Lines(
        "---",
        "section: 1",
        "title: Introduction",
        "scope: TODO — one-line scope shown in the README document set.",
        "summary: TODO — a lead paragraph introducing this interface.",
        "crossReferences:",
        "  - \"[Overview](detailed-interface-definition/overview.md) — boundary model.\"",
        "---",
        "",
        "## 1.1 Purpose",
        "",
        "<!-- Describe what this library publishes and the binding contract. -->",
        "TODO — purpose.",
        "",
        "## 1.2 Conventions",
        "",
        "<!-- e.g. interfaces use the I… prefix; tokens use SCREAMING_SNAKE_CASE. -->",
        "TODO — conventions.");

    private static string Overview(string slug) => Lines(
        "---",
        "section: 3",
        "title: Detailed Interface Definition — Overview",
        "scope: TODO — boundary model and the master interface inventory.",
        "summary: TODO — a one-paragraph architectural overview.",
        "crossReferences:",
        $"  - \"[Services]({slug}/core-api/services.md)\"",
        $"  - \"[Functions]({slug}/core-api/functions.md)\"",
        $"  - \"[Data Object Definitions]({slug}/data-object-definitions.md)\"",
        "---",
        "",
        "## 3.1 Boundary model",
        "",
        "<!-- How consumers depend on this library's contracts. -->",
        "TODO — boundary model.");

    private static string Services() => Lines(
        "---",
        "template: services",
        "section: 8",
        "title: Core API — Services",
        "scope: TODO — every injectable service contract (interface + token + class).",
        "summary: TODO — a lead paragraph for the services section.",
        "inventory: true",
        "# --- Add one group per capability; bind real exported symbol names. ---",
        "# interfaces, tokens and classes pair by position (i-th with i-th).",
        "# groups:",
        "#   - title: My capability",
        "#     heading: \"8.1\"",
        "#     access: Action            # free text: Read / Action / …",
        "#     intro: One-line intro under the heading.",
        "#     interfaces: [IMyService]",
        "#     tokens: [MY_SERVICE]",
        "#     classes: [MyService]",
        "requirements:",
        "  - TODO — a binding **shall** statement.",
        "crossReferences:",
        "  - \"[Functions](./functions.md)\"",
        "  - \"[Data Object Definitions](../data-object-definitions.md)\"",
        "---",
        "",
        "<!-- add services here: uncomment and fill in `groups:` above, then run `surfaceq ficd` -->");

    private static string Functions() => Lines(
        "---",
        "template: functions",
        "section: 7",
        "title: Core API — Functions",
        "scope: TODO — standalone, non-injectable functions.",
        "summary: TODO — a lead paragraph for the functions section.",
        "inventory: true",
        "# groups:",
        "#   - title: Composition",
        "#     heading: \"7.1\"",
        "#     intro: One-line intro under the heading.",
        "#     functions: [provideMyLib]",
        "crossReferences:",
        "  - \"[Services](./services.md)\"",
        "---",
        "",
        "<!-- add functions here: list exported function names under a group's `functions:` -->");

    private static string DataObjects() => Lines(
        "---",
        "template: data-objects",
        "section: 9",
        "title: Data Object Definitions",
        "scope: TODO — every public data shape and literal-union type.",
        "summary: TODO — a lead paragraph for the data objects section.",
        "# groups:",
        "#   - title: My shapes",
        "#     heading: \"9.1\"",
        "#     intro: One-line intro under the heading.",
        "#     dataObjects: [MyRecord, MyRequest]   # interfaces -> field tables + TypeScript",
        "#     types: [MyUnion]                     # type aliases -> value-set table",
        "crossReferences:",
        "  - \"[Services](core-api/services.md)\"",
        "---",
        "",
        "<!-- add data objects here: list exported interface/type names under a group -->");

    // kebab-cases a library name: lowercase, runs of non-alphanumerics collapse to
    // a single hyphen, leading/trailing hyphens trimmed (e.g. "@acme/Auth" -> "acme-auth").
    private static string Slugify(string name)
    {
        var sb = new StringBuilder();
        var pendingHyphen = false;
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                if (pendingHyphen && sb.Length > 0)
                {
                    sb.Append('-');
                }
                sb.Append(c);
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = true;
            }
        }
        var slug = sb.ToString();
        return slug.Length > 0 ? slug : "library";
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";
}
