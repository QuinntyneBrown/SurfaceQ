using System.Text;

namespace SurfaceQ.Core;

// Renders a LibraryApi to a Markdown document with tables. Output is
// deterministic: declarations are sorted by name within each kind, members
// keep their source order. Pipe characters in type text are escaped so they
// never break a table cell.

public sealed class MarkdownRenderer
{
    public string Render(LibraryApi library)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(library.Name).Append(" — Public API\n\n");

        var sections = BuildSections(library.Declarations);
        if (sections.Count == 0)
        {
            sb.Append("_This library exposes no public API._\n");
            return sb.ToString();
        }

        var deprecations = CollectDeprecations(library.Declarations);
        AppendContents(sb, sections, deprecations.Count > 0);
        if (deprecations.Count > 0)
        {
            AppendDeprecationsSummary(sb, deprecations);
        }
        foreach (var section in sections)
        {
            section.Append(sb);
        }
        return sb.ToString();
    }

    private static List<Section> BuildSections(IReadOnlyList<ApiDeclaration> declarations)
    {
        var sections = new List<Section>();
        AddSection(sections, "Interfaces", declarations, "interface", AppendTypeBlock);
        AddSection(sections, "Injection Tokens", declarations, "injection-token", AppendTokensTable);
        AddSection(sections, "Enums", declarations, "enum", AppendEnumBlock);
        AddSection(sections, "Type Aliases", declarations, "type", AppendTypeAliasTable);
        AddSection(sections, "Classes", declarations, "class", AppendTypeBlock);
        AddSection(sections, "Functions", declarations, "function", AppendFunctionTable);
        AddSection(sections, "Constants", declarations, "const", AppendConstTable);
        return sections;
    }

    private static void AddSection(
        List<Section> sections,
        string title,
        IReadOnlyList<ApiDeclaration> all,
        string kind,
        Action<StringBuilder, IReadOnlyList<ApiDeclaration>> body)
    {
        var matching = all
            .Where(d => d.Kind == kind)
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();
        if (matching.Count > 0)
        {
            sections.Add(new Section(title, matching, body));
        }
    }

    private static void AppendContents(StringBuilder sb, List<Section> sections, bool hasDeprecations)
    {
        sb.Append("## Contents\n\n");
        if (hasDeprecations)
        {
            sb.Append("- [Deprecations](#deprecations)\n");
        }
        foreach (var section in sections)
        {
            sb.Append("- [").Append(section.Title).Append("](#").Append(Anchor(section.Title)).Append(")\n");
        }
        sb.Append('\n');
    }

    private static void AppendDeprecationsSummary(StringBuilder sb, List<DeprecatedRef> deprecations)
    {
        sb.Append("## Deprecations\n\n");
        sb.Append("| Item | Kind | Reason |\n");
        sb.Append("| --- | --- | --- |\n");
        foreach (var d in deprecations)
        {
            sb.Append("| ").Append(Code(d.Item))
              .Append(" | ").Append(d.Kind)
              .Append(" | ").Append(Cell(d.Reason))
              .Append(" |\n");
        }
        sb.Append('\n');
    }

    // Flattens whole-declaration and member-level deprecations into one ordered
    // list for the summary table. Declarations are name-ordered; within each,
    // the declaration precedes its members (source order) for determinism.
    private static List<DeprecatedRef> CollectDeprecations(IReadOnlyList<ApiDeclaration> declarations)
    {
        var refs = new List<DeprecatedRef>();
        foreach (var d in declarations.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            if (d.Deprecated)
            {
                refs.Add(new DeprecatedRef(d.Name, d.Kind, d.DeprecationReason));
            }
            foreach (var m in d.Members)
            {
                if (m.Deprecated)
                {
                    refs.Add(new DeprecatedRef($"{d.Name}.{m.Name}", m.MemberKind, m.DeprecationReason));
                }
            }
            foreach (var e in d.EnumMembers)
            {
                if (e.Deprecated)
                {
                    refs.Add(new DeprecatedRef($"{d.Name}.{e.Name}", "enum member", e.DeprecationReason));
                }
            }
        }
        return refs;
    }

    // --- Interfaces and classes -------------------------------------------

    private static void AppendTypeBlock(StringBuilder sb, IReadOnlyList<ApiDeclaration> declarations)
    {
        foreach (var d in declarations)
        {
            sb.Append("### `").Append(d.Name).Append("`\n\n");
            AppendDeprecationCallout(sb, d.Deprecated, d.DeprecationReason);
            AppendDocLine(sb, d.Doc);
            AppendHeritage(sb, "Extends", d.Extends);
            AppendHeritage(sb, "Implements", d.Implements);

            var properties = d.Members.Where(m => m.MemberKind == "property").ToList();
            var methods = d.Members.Where(m => m.MemberKind == "method").ToList();
            if (properties.Count == 0 && methods.Count == 0)
            {
                sb.Append("_No public members._\n\n");
                continue;
            }
            AppendPropertyTable(sb, properties);
            AppendMethodTable(sb, methods);
        }
    }

    private static void AppendPropertyTable(StringBuilder sb, List<ApiMember> properties)
    {
        if (properties.Count == 0)
        {
            return;
        }
        sb.Append("**Properties**\n\n");
        sb.Append("| Name | Type | Optional | Deprecated | Description |\n");
        sb.Append("| --- | --- | --- | --- | --- |\n");
        foreach (var p in properties)
        {
            var name = p.Readonly ? "readonly " + p.Name : p.Name;
            sb.Append("| ").Append(Code(name))
              .Append(" | ").Append(Code(p.Type))
              .Append(" | ").Append(p.Optional ? "yes" : "no")
              .Append(" | ").Append(DeprecatedCell(p.Deprecated, p.DeprecationReason))
              .Append(" | ").Append(Cell(p.Doc))
              .Append(" |\n");
        }
        sb.Append('\n');
    }

    private static void AppendMethodTable(StringBuilder sb, List<ApiMember> methods)
    {
        if (methods.Count == 0)
        {
            return;
        }
        sb.Append("**Methods**\n\n");
        sb.Append("| Method | Parameters | Returns | Deprecated | Description |\n");
        sb.Append("| --- | --- | --- | --- | --- |\n");
        foreach (var m in methods)
        {
            var name = m.Optional ? m.Name + "?" : m.Name;
            sb.Append("| ").Append(Code(name))
              .Append(" | ").Append(Code(FormatParameters(m.Parameters)))
              .Append(" | ").Append(Code(m.ReturnType))
              .Append(" | ").Append(DeprecatedCell(m.Deprecated, m.DeprecationReason))
              .Append(" | ").Append(Cell(m.Doc))
              .Append(" |\n");
        }
        sb.Append('\n');
    }

    // --- Other kinds -------------------------------------------------------

    private static void AppendTokensTable(StringBuilder sb, IReadOnlyList<ApiDeclaration> declarations)
    {
        sb.Append("Inject these tokens and depend on the contract type, not a concrete implementation.\n\n");
        sb.Append("| Token | Contract | Deprecated | Description |\n");
        sb.Append("| --- | --- | --- | --- |\n");
        foreach (var d in declarations)
        {
            var description = !string.IsNullOrEmpty(d.Doc) ? d.Doc : d.Description;
            sb.Append("| ").Append(Code(d.Name))
              .Append(" | ").Append(Code(d.Contract))
              .Append(" | ").Append(DeprecatedCell(d.Deprecated, d.DeprecationReason))
              .Append(" | ").Append(Cell(description))
              .Append(" |\n");
        }
        sb.Append('\n');
    }

    private static void AppendEnumBlock(StringBuilder sb, IReadOnlyList<ApiDeclaration> declarations)
    {
        foreach (var d in declarations)
        {
            sb.Append("### `").Append(d.Name).Append("`\n\n");
            AppendDeprecationCallout(sb, d.Deprecated, d.DeprecationReason);
            AppendDocLine(sb, d.Doc);
            sb.Append("| Member | Value | Deprecated | Description |\n");
            sb.Append("| --- | --- | --- | --- |\n");
            foreach (var m in d.EnumMembers)
            {
                sb.Append("| ").Append(Code(m.Name))
                  .Append(" | ").Append(Code(m.Value))
                  .Append(" | ").Append(DeprecatedCell(m.Deprecated, m.DeprecationReason))
                  .Append(" | ").Append(Cell(m.Doc))
                  .Append(" |\n");
            }
            sb.Append('\n');
        }
    }

    private static void AppendTypeAliasTable(StringBuilder sb, IReadOnlyList<ApiDeclaration> declarations)
    {
        sb.Append("| Name | Definition | Deprecated | Description |\n");
        sb.Append("| --- | --- | --- | --- |\n");
        foreach (var d in declarations)
        {
            sb.Append("| ").Append(Code(d.Name))
              .Append(" | ").Append(Code(d.Definition))
              .Append(" | ").Append(DeprecatedCell(d.Deprecated, d.DeprecationReason))
              .Append(" | ").Append(Cell(d.Doc))
              .Append(" |\n");
        }
        sb.Append('\n');
    }

    private static void AppendFunctionTable(StringBuilder sb, IReadOnlyList<ApiDeclaration> declarations)
    {
        sb.Append("| Function | Parameters | Returns | Deprecated | Description |\n");
        sb.Append("| --- | --- | --- | --- | --- |\n");
        foreach (var d in declarations)
        {
            sb.Append("| ").Append(Code(d.Name))
              .Append(" | ").Append(Code(FormatParameters(d.Parameters)))
              .Append(" | ").Append(Code(d.ReturnType))
              .Append(" | ").Append(DeprecatedCell(d.Deprecated, d.DeprecationReason))
              .Append(" | ").Append(Cell(d.Doc))
              .Append(" |\n");
        }
        sb.Append('\n');
    }

    private static void AppendConstTable(StringBuilder sb, IReadOnlyList<ApiDeclaration> declarations)
    {
        sb.Append("| Name | Type | Deprecated | Description |\n");
        sb.Append("| --- | --- | --- | --- |\n");
        foreach (var d in declarations)
        {
            sb.Append("| ").Append(Code(d.Name))
              .Append(" | ").Append(Code(d.Type))
              .Append(" | ").Append(DeprecatedCell(d.Deprecated, d.DeprecationReason))
              .Append(" | ").Append(Cell(d.Doc))
              .Append(" |\n");
        }
        sb.Append('\n');
    }

    // --- Shared formatting -------------------------------------------------

    private static void AppendDocLine(StringBuilder sb, string doc)
    {
        if (!string.IsNullOrEmpty(doc))
        {
            sb.Append(doc).Append("\n\n");
        }
    }

    private static void AppendHeritage(StringBuilder sb, string label, IReadOnlyList<string> names)
    {
        if (names.Count > 0)
        {
            sb.Append("_").Append(label).Append(": ")
              .Append(string.Join(", ", names.Select(Code)))
              .Append("_\n\n");
        }
    }

    private static string FormatParameters(IReadOnlyList<ApiParameter> parameters)
    {
        if (parameters.Count == 0)
        {
            return "()";
        }
        return string.Join(", ", parameters.Select(p =>
            p.Optional ? $"{p.Name}?: {p.Type}" : $"{p.Name}: {p.Type}"));
    }

    private static string Code(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "–";
        }
        return CodeSpan(EscapePipes(value));
    }

    // Wraps text in a Markdown code span whose backtick fence is one longer than
    // the longest backtick run inside, padding with a space when the text begins
    // or ends with a backtick. A value such as a template-literal type
    // (`a-${string}`) therefore cannot close the span early and spill the row.
    private static string CodeSpan(string text)
    {
        var fence = new string('`', LongestBacktickRun(text) + 1);
        var pad = text[0] == '`' || text[^1] == '`' ? " " : "";
        return fence + pad + text + pad + fence;
    }

    private static int LongestBacktickRun(string text)
    {
        var longest = 0;
        var current = 0;
        foreach (var c in text)
        {
            current = c == '`' ? current + 1 : 0;
            longest = current > longest ? current : longest;
        }
        return longest;
    }

    private static string Cell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "–";
        }
        return EscapePipes(value);
    }

    private static string DeprecatedCell(bool deprecated, string reason)
    {
        if (!deprecated)
        {
            return "no";
        }
        return string.IsNullOrEmpty(reason) ? "yes" : EscapePipes(reason);
    }

    private static void AppendDeprecationCallout(StringBuilder sb, bool deprecated, string reason)
    {
        if (!deprecated)
        {
            return;
        }
        sb.Append("> ⚠️ **Deprecated**");
        if (!string.IsNullOrEmpty(reason))
        {
            sb.Append(" — ").Append(reason);
        }
        sb.Append("\n\n");
    }

    private static string EscapePipes(string value) => value.Replace("|", "\\|");

    private static string Anchor(string title)
    {
        var sb = new StringBuilder();
        foreach (var c in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (c == ' ')
            {
                sb.Append('-');
            }
        }
        return sb.ToString();
    }

    private sealed record DeprecatedRef(string Item, string Kind, string Reason);

    private sealed record Section(
        string Title,
        IReadOnlyList<ApiDeclaration> Declarations,
        Action<StringBuilder, IReadOnlyList<ApiDeclaration>> Body)
    {
        public void Append(StringBuilder sb)
        {
            sb.Append("## ").Append(Title).Append("\n\n");
            Body(sb, Declarations);
        }
    }
}
