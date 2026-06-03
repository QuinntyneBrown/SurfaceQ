// Acceptance Test
// Traces to: L2-024, L2-025, L2-026
// Description: MarkdownRenderer emits tables for interfaces (properties + methods),
// injection tokens (contract type), enums, and type aliases; escapes pipe
// characters; lists only non-empty sections; and sorts declarations by name.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Renders_interface_with_properties_and_methods()
    {
        var iface = Decl("Principal", "interface", doc: "A principal.", members: new[]
        {
            Property("id", "string", optional: false, isReadonly: true, doc: "The id."),
            Property("name", "string", optional: true, isReadonly: false, doc: ""),
            Method("hasRole", "boolean", doc: "Checks a role.",
                new ApiParameter("role", "Role | string", false)),
        });

        var md = new MarkdownRenderer().Render(new LibraryApi("@acme/auth", new[] { iface }));

        Assert.Contains("# @acme/auth — Public API", md);
        Assert.Contains("## Interfaces", md);
        Assert.Contains("### `Principal`", md);
        Assert.Contains("| `readonly id` | `string` | no | no | The id. |", md);
        Assert.Contains("| `name` | `string` | yes | no | – |", md);
        // Pipe inside the parameter type is escaped so it does not break the cell.
        Assert.Contains("| `hasRole` | `role: Role \\| string` | `boolean` | no | Checks a role. |", md);
    }

    [Fact]
    public void Renders_injection_token_with_contract_type()
    {
        var token = Decl("AUTH_SERVICE", "injection-token");
        token = token with { Contract = "AuthService", Description = "the auth backend" };

        var md = new MarkdownRenderer().Render(new LibraryApi("lib", new[] { token }));

        Assert.Contains("## Injection Tokens", md);
        Assert.Contains("| `AUTH_SERVICE` | `AuthService` | no | the auth backend |", md);
    }

    [Fact]
    public void Renders_enum_and_type_alias()
    {
        var role = Decl("Role", "enum");
        role = role with
        {
            EnumMembers = new[]
            {
                new EnumMember("Guest", "0", "", false, ""),
                new EnumMember("Admin", "10", "", false, ""),
            },
        };
        var alias = Decl("AuthState", "type");
        alias = alias with { Definition = "string | Principal", Doc = "Auth state." };

        var md = new MarkdownRenderer().Render(new LibraryApi("lib", new[] { role, alias }));

        Assert.Contains("### `Role`", md);
        Assert.Contains("| `Guest` | `0` | no | – |", md);
        Assert.Contains("| `AuthState` | `string \\| Principal` | no | Auth state. |", md);
    }

    [Fact]
    public void Contents_lists_only_non_empty_sections_and_sorts_declarations()
    {
        var zebra = Decl("Zebra", "interface");
        var apple = Decl("Apple", "interface");

        var md = new MarkdownRenderer().Render(new LibraryApi("lib", new[] { zebra, apple }));

        Assert.Contains("- [Interfaces](#interfaces)", md);
        Assert.DoesNotContain("- [Enums]", md);
        Assert.True(md.IndexOf("### `Apple`", StringComparison.Ordinal)
                    < md.IndexOf("### `Zebra`", StringComparison.Ordinal));
    }

    [Fact]
    public void Renders_deprecated_column_and_callout_and_summary()
    {
        var alias = Decl("OldId", "type", deprecated: true, deprecationReason: "use Id")
            with { Definition = "string" };
        var iface = Decl("LegacyBill", "interface", doc: "Legacy.", members: new[]
        {
            Property("total", "number", optional: false, isReadonly: true, doc: "",
                deprecated: true, deprecationReason: "use amount"),
            Property("amount", "number", optional: false, isReadonly: false, doc: ""),
        });
        iface = iface with { Deprecated = true, DeprecationReason = "use Bill" };

        var md = new MarkdownRenderer().Render(new LibraryApi("lib", new[] { alias, iface }));

        // Summary section + contents entry.
        Assert.Contains("- [Deprecations](#deprecations)", md);
        Assert.Contains("## Deprecations", md);
        Assert.Contains("| `LegacyBill` | interface | use Bill |", md);
        Assert.Contains("| `LegacyBill.total` | property | use amount |", md);
        Assert.Contains("| `OldId` | type | use Id |", md);
        // Heading callout for the whole interface.
        Assert.Contains("> ⚠️ **Deprecated** — use Bill", md);
        // Column in the type-alias table and the member table.
        Assert.Contains("| Name | Definition | Deprecated | Description |", md);
        Assert.Contains("| `OldId` | `string` | use Id | – |", md);
        Assert.Contains("| `readonly total` | `number` | no | use amount | – |", md);
        Assert.Contains("| `amount` | `number` | no | no | – |", md);
    }

    [Fact]
    public void Omits_deprecations_section_when_nothing_is_deprecated()
    {
        var md = new MarkdownRenderer().Render(new LibraryApi("lib", new[] { Decl("Id", "type") }));
        Assert.DoesNotContain("## Deprecations", md);
        Assert.DoesNotContain("Deprecations](#deprecations)", md);
        // Column header is still present (always rendered), value is "no".
        Assert.Contains("| Name | Definition | Deprecated | Description |", md);
    }

    [Fact]
    public void Empty_library_states_no_public_api()
    {
        var md = new MarkdownRenderer().Render(new LibraryApi("lib", Array.Empty<ApiDeclaration>()));
        Assert.Contains("_This library exposes no public API._", md);
    }

    [Fact]
    public void Code_span_fences_values_that_contain_backticks()
    {
        // A template-literal type carries backticks; a naive single-backtick span
        // would close early and spill the rest of the row. The fence must widen
        // and pad so the value stays a single intact code span.
        var alias = Decl("Slug", "type") with { Definition = "`a-${string}`" };

        var md = new MarkdownRenderer().Render(new LibraryApi("lib", new[] { alias }));

        Assert.Contains("| `Slug` | `` `a-${string}` `` | no | – |", md);
    }

    private static ApiDeclaration Decl(
        string name,
        string kind,
        string doc = "",
        IReadOnlyList<ApiMember>? members = null,
        bool deprecated = false,
        string deprecationReason = "") =>
        new(
            Name: name,
            Kind: kind,
            Doc: doc,
            Definition: "",
            Contract: "",
            Description: "",
            Type: "",
            ReturnType: "",
            Extends: Array.Empty<string>(),
            Implements: Array.Empty<string>(),
            Parameters: Array.Empty<ApiParameter>(),
            Members: members ?? Array.Empty<ApiMember>(),
            EnumMembers: Array.Empty<EnumMember>(),
            Deprecated: deprecated,
            DeprecationReason: deprecationReason);

    private static ApiMember Property(
        string name, string type, bool optional, bool isReadonly, string doc,
        bool deprecated = false, string deprecationReason = "") =>
        new("property", name, type, "", optional, isReadonly, Array.Empty<ApiParameter>(), doc,
            deprecated, deprecationReason);

    private static ApiMember Method(string name, string returnType, string doc, params ApiParameter[] parameters) =>
        new("method", name, "", returnType, false, false, parameters, doc, false, "");
}
