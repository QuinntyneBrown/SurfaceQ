// Acceptance Test
// Traces to: L2-036, L2-037, L2-038
// Description: FicdRenderer emits an identity block per document (non-empty
// manifest fields only), a services inventory + member tables, data-object field
// tables and reconstructed TypeScript, requirements and cross-references, a README
// with a Document Set table, deterministic and pipe-safe output, and a visible
// placeholder for symbols missing from the extracted API.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class FicdRendererTests
{
    private const string ServicesPath = "detailed-interface-definition/core-api/services.md";
    private const string DataPath = "detailed-interface-definition/data-object-definitions.md";

    [Fact]
    public void Services_template_renders_identity_inventory_and_member_tables()
    {
        // INotThere is bound by the group below but deliberately absent from the
        // extracted API, to exercise the missing-symbol placeholder path.
        var api = Api(
            Iface("ICommandService", "Sends outbound commands.",
                Method("send", "Promise<void>", "Sends a command.", new ApiParameter("command", "TCommand", false))));

        var section = Section(
            ServicesPath, "services", 8, "Core API — Services",
            scope: "All plugin-facing services.",
            summary: "This section specifies every plugin-facing service.",
            inventory: true,
            groups: new[]
            {
                Group("Outbound messaging — Command", "8.3", "These services emit messages.", "Action",
                    interfaces: new[] { "ICommandService", "INotThere" },
                    tokens: new[] { "COMMAND_SERVICE" },
                    classes: new[] { "CommandService" }),
            },
            requirements: new[] { "A plugin **shall** inject the `InjectionToken`, not the class." },
            crossRefs: new[] { "[Functions](./functions.md) — provider functions." });

        var manifest = Manifest("MDF FICD", "1.0 (Draft)", ServicesPath);
        var files = new FicdRenderer().Render(new FicdProject("@gers/mdf", manifest, new[] { section }), api);
        var md = Content(files, ServicesPath);

        // Identity block: present fields rendered, empty fields omitted.
        Assert.Contains("# Core API — Services", md);
        Assert.Contains("| Document Title | MDF FICD |", md);
        Assert.Contains("| Section | Core API — Services |", md);
        Assert.Contains("| Version | 1.0 (Draft) |", md);
        Assert.DoesNotContain("| Date |", md);
        Assert.Contains("This section specifies every plugin-facing service.", md);

        // Inventory table (inventory: true) precedes the groups.
        Assert.Contains(
            "| Outbound messaging — Command | `ICommandService` | `COMMAND_SERVICE` | `CommandService` | Action |",
            md);
        // Second interface has no token/class -> en-dash cells.
        Assert.Contains("| Outbound messaging — Command | `INotThere` | – | – | Action |", md);

        // Group heading + per-interface member table.
        Assert.Contains("## 8.3 Outbound messaging — Command", md);
        Assert.Contains("### `ICommandService` / `COMMAND_SERVICE`", md);
        Assert.Contains("Concrete class: `CommandService`.", md);
        Assert.Contains("| `send` | Method | `send(command: TCommand): Promise<void>` | Sends a command. |", md);

        // Missing symbol renders a visible placeholder, not a crash.
        Assert.Contains("_(not found: INotThere)_", md);

        // Requirements + cross-references; the relative link survives verbatim.
        Assert.Contains("1. A plugin **shall** inject the `InjectionToken`, not the class.", md);
        Assert.Contains("- [Functions](./functions.md) — provider functions.", md);
    }

    [Fact]
    public void Data_objects_template_renders_fields_table_and_typescript()
    {
        var api = Api(
            Iface("CuiRecord", "A telemetry sample.",
                Property("cui", "string", optional: false, readOnly: false, "The CUI."),
                Property("value", "number | string | boolean", optional: false, readOnly: false, ""),
                Property("ust", "number", optional: true, readOnly: false, "Timestamp.")),
            TypeAlias("CuiStatus", "'fresh' | 'stale' | 'missing'"));

        var section = Section(
            DataPath, "data-objects", 9, "Data Object Definitions",
            scope: "Every public data shape.", summary: "", inventory: false,
            groups: new[]
            {
                Group("Telemetry", "9.3", "", "",
                    types: new[] { "CuiStatus" },
                    dataObjects: new[] { "CuiRecord" }),
            });

        var files = new FicdRenderer().Render(
            new FicdProject("@gers/mdf", Manifest("MDF FICD", "1.0", DataPath), new[] { section }), api);
        var md = Content(files, DataPath);

        Assert.Contains("### `CuiRecord`", md);
        Assert.Contains("| `cui` | `string` | Yes | The CUI. |", md);
        // Optional property -> Required "No"; pipe in the union type is escaped.
        Assert.Contains("| `value` | `number \\| string \\| boolean` | Yes | – |", md);
        Assert.Contains("| `ust` | `number` | No | Timestamp. |", md);

        // Reconstructed TypeScript (raw types inside a fenced block — no escaping).
        Assert.Contains("```typescript", md);
        Assert.Contains("interface CuiRecord {", md);
        Assert.Contains("  value: number | string | boolean;", md);
        Assert.Contains("  ust?: number;", md);

        // Value-set table for the bound type alias.
        Assert.Contains("| `CuiStatus` | `'fresh' \\| 'stale' \\| 'missing'` |", md);
    }

    [Fact]
    public void Functions_template_renders_inventory_and_parameter_tables()
    {
        var api = Api(
            Func("describeError", "string", "Normalizes a thrown value.",
                new ApiParameter("err", "unknown", false), new ApiParameter("fallback", "string", false)));

        var section = Section(
            "functions.md", "functions", 7, "Functions", "Standalone functions.", "", inventory: true,
            groups: new[] { Group("Utilities", "7.1", "", "", functions: new[] { "describeError" }) });

        var files = new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", "functions.md"), new[] { section }), api);
        var md = Content(files, "functions.md");

        Assert.Contains("| Function | Signature | Description |", md);
        Assert.Contains(
            "| `describeError` | `describeError(err: unknown, fallback: string): string` | Normalizes a thrown value. |",
            md);
        Assert.Contains("## 7.1 Utilities", md);
        Assert.Contains("| `describeError` | `err: unknown, fallback: string` | `string` | Normalizes a thrown value. |", md);
    }

    [Fact]
    public void Readme_lists_documents_in_manifest_order()
    {
        var services = Section(ServicesPath, "services", 8, "Core API — Services", "All services.", "", false);
        var intro = Section("introduction.md", "narrative", 1, "Introduction", "Purpose and scope.", "", false);
        // Manifest lists introduction first, then services.
        var manifest = Manifest("MDF FICD", "1.0", "introduction.md", ServicesPath);

        var files = new FicdRenderer().Render(
            new FicdProject("@gers/mdf", manifest, new[] { intro, services }), Api());
        var readme = Content(files, "README.md");

        Assert.Contains("# MDF FICD", readme);
        Assert.Contains("## Document Set", readme);
        Assert.Contains("| 1 | [Introduction](introduction.md) | Purpose and scope. |", readme);
        Assert.Contains(
            $"| 2 | [Core API — Services]({ServicesPath}) | All services. |", readme);
        // README identity block has no Section row.
        Assert.DoesNotContain("| Section |", readme);
    }

    [Fact]
    public void Output_is_deterministic_across_repeated_renders()
    {
        var api = Api(Iface("IThing", "A thing.", Method("go", "void", "Go.")));
        var section = Section(
            ServicesPath, "services", 8, "Services", "", "", true,
            groups: new[] { Group("Group", "8.1", "", "Action", interfaces: new[] { "IThing" }, tokens: new[] { "THING" }) });
        var project = new FicdProject("lib", Manifest("FICD", "1.0", ServicesPath), new[] { section });

        var renderer = new FicdRenderer();
        var first = renderer.Render(project, api);
        var second = renderer.Render(project, api);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].RelativePath, second[i].RelativePath);
            Assert.Equal(first[i].Content, second[i].Content);
        }
    }

    [Fact]
    public void Every_document_ends_with_exactly_one_trailing_newline()
    {
        var section = Section("introduction.md", "narrative", 1, "Introduction", "", "", false, body: "Some prose.");
        var files = new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", "introduction.md"), new[] { section }), Api());

        foreach (var file in files)
        {
            Assert.EndsWith("\n", file.Content);
            Assert.False(file.Content.EndsWith("\n\n", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Narrative_template_passes_the_body_through_with_relative_links_verbatim()
    {
        var section = Section("introduction.md", "narrative", 1, "Introduction", "", "Purpose and scope.", false,
            body: "See [Functions](./functions.md) for providers.");

        var md = Content(new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", "introduction.md"), new[] { section }), Api()),
            "introduction.md");

        Assert.Contains("Purpose and scope.", md);
        Assert.Contains("See [Functions](./functions.md) for providers.", md);
    }

    [Fact]
    public void Symbol_table_keeps_the_first_declaration_on_a_name_collision()
    {
        var api = Api(
            Iface("IThing", "First declaration.", Method("alpha", "void", "Alpha.")),
            Iface("IThing", "Second declaration.", Method("omega", "void", "Omega.")));
        var section = Section(ServicesPath, "services", 8, "Services", "", "", false,
            groups: new[] { Group("G", "8.1", "", "", interfaces: new[] { "IThing" }) });

        var md = Content(new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", ServicesPath), new[] { section }), api), ServicesPath);

        Assert.Contains("First declaration.", md);
        Assert.Contains("| `alpha` |", md);
        Assert.DoesNotContain("omega", md);
    }

    [Fact]
    public void Readme_appends_sections_not_listed_in_the_manifest_after_the_listed_ones()
    {
        var alpha = Section("a.md", "narrative", 1, "Alpha", "First scope.", "", false);
        var zeta = Section("z.md", "narrative", 2, "Zeta", "Last scope.", "", false);
        // Manifest lists only z.md; a.md is unlisted and must follow, sorted by path.
        var readme = Content(new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", "z.md"), new[] { alpha, zeta }), Api()), "README.md");

        Assert.Contains("| 1 | [Zeta](z.md) | Last scope. |", readme);
        Assert.Contains("| 2 | [Alpha](a.md) | First scope. |", readme);
        Assert.True(readme.IndexOf("[Zeta]") < readme.IndexOf("[Alpha]"));
    }

    [Fact]
    public void Readme_states_when_there_are_no_section_documents()
    {
        var readme = Content(new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0"), Array.Empty<FicdSection>()), Api()), "README.md");

        Assert.Contains("_This FICD has no section documents._", readme);
    }

    [Fact]
    public void Readme_wraps_a_link_target_containing_a_space_in_angle_brackets()
    {
        var section = Section("core api (v2).md", "narrative", 1, "Core API", "Scope.", "", false);

        var readme = Content(new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", "core api (v2).md"), new[] { section }), Api()), "README.md");

        Assert.Contains("[Core API](<core api (v2).md>)", readme);
    }

    [Fact]
    public void Missing_function_and_value_set_symbols_are_named_in_the_placeholder()
    {
        var functions = Section("functions.md", "functions", 7, "Functions", "", "", inventory: true,
            groups: new[] { Group("G", "7.1", "", "", functions: new[] { "ghostFn" }) });
        var fmd = Content(new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", "functions.md"), new[] { functions }), Api()), "functions.md");
        Assert.Contains("_(not found: ghostFn)_", fmd);

        var data = Section(DataPath, "data-objects", 9, "Data", "", "", false,
            groups: new[] { Group("G", "9.1", "", "", types: new[] { "PhantomType" }) });
        var dmd = Content(new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", DataPath), new[] { data }), Api()), DataPath);
        Assert.Contains("_(not found: PhantomType)_", dmd);
    }

    [Fact]
    public void Inventory_table_is_omitted_when_no_rows_would_be_emitted()
    {
        // inventory: true but the group binds no interfaces -> no header-only table.
        var section = Section(ServicesPath, "services", 8, "Services", "", "", inventory: true,
            groups: new[] { Group("Just prose", "8.1", "Narrative only.", "Read") });

        var md = Content(new FicdRenderer().Render(
            new FicdProject("lib", Manifest("FICD", "1.0", ServicesPath), new[] { section }), Api()), ServicesPath);

        Assert.DoesNotContain("| Capability | Interface | Token | Concrete class | Access |", md);
        Assert.Contains("## 8.1 Just prose", md);
    }

    // --- builders ----------------------------------------------------------

    private static string Content(IReadOnlyList<FicdOutputFile> files, string path) =>
        files.First(f => f.RelativePath == path).Content;

    private static LibraryApi Api(params ApiDeclaration[] declarations) =>
        new("lib", declarations);

    private static ApiDeclaration Iface(string name, string doc, params ApiMember[] members) =>
        new(name, "interface", doc, "", "", "", "", "", Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ApiParameter>(), members, Array.Empty<EnumMember>(), false, "");

    private static ApiDeclaration TypeAlias(string name, string definition) =>
        new(name, "type", "", definition, "", "", "", "", Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ApiParameter>(), Array.Empty<ApiMember>(), Array.Empty<EnumMember>(), false, "");

    private static ApiDeclaration Func(string name, string returnType, string doc, params ApiParameter[] parameters) =>
        new(name, "function", doc, "", "", "", "", returnType, Array.Empty<string>(), Array.Empty<string>(),
            parameters, Array.Empty<ApiMember>(), Array.Empty<EnumMember>(), false, "");

    private static ApiMember Method(string name, string returnType, string doc, params ApiParameter[] parameters) =>
        new("method", name, "", returnType, false, false, parameters, doc, false, "");

    private static ApiMember Property(string name, string type, bool optional, bool readOnly, string doc) =>
        new("property", name, type, "", optional, readOnly, Array.Empty<ApiParameter>(), doc, false, "");

    private static FicdGroup Group(
        string title, string heading, string intro, string access,
        string[]? interfaces = null, string[]? tokens = null, string[]? classes = null,
        string[]? functions = null, string[]? types = null, string[]? dataObjects = null) =>
        new(title, heading, intro, access,
            interfaces ?? Array.Empty<string>(), tokens ?? Array.Empty<string>(), classes ?? Array.Empty<string>(),
            functions ?? Array.Empty<string>(), types ?? Array.Empty<string>(), dataObjects ?? Array.Empty<string>());

    private static FicdSection Section(
        string path, string template, int section, string title, string scope, string summary, bool inventory,
        FicdGroup[]? groups = null, string[]? requirements = null, string[]? crossRefs = null, string body = "") =>
        new(path, template, section, title, scope, summary, inventory,
            groups ?? Array.Empty<FicdGroup>(), requirements ?? Array.Empty<string>(),
            crossRefs ?? Array.Empty<string>(), body);

    private static FicdManifest Manifest(string title, string version, params string[] documents) =>
        new(title, "", version, "", "", "", "", "", documents);
}
