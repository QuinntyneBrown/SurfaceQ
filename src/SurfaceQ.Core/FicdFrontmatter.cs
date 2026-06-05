namespace SurfaceQ.Core;

// Parses the constrained YAML subset used by the FICD authoring schema
// (docs/specs/ficd-schema.md). SurfaceQ ships no YAML dependency, so this reads
// only the documented forms: scalars, quoted scalars, block lists (- item),
// inline lists ([a, b]), comments (#), and the single list-of-maps key `groups`
// whose item fields are scalars or inline lists. Anything richer belongs in the
// Markdown body, which Split() returns untouched.

public static class FicdFrontmatter
{
    // Separates a section file's leading `--- ... ---` frontmatter from its body.
    // A file with no leading fence has empty frontmatter and is all body.
    public static (string Frontmatter, string Body) Split(string text)
    {
        var lines = Normalize(text).Split('\n');
        if (lines.Length == 0 || lines[0] != "---")
        {
            return ("", Normalize(text));
        }
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i] == "---")
            {
                var body = i + 1 < lines.Length ? string.Join("\n", lines[(i + 1)..]) : "";
                return (string.Join("\n", lines[1..i]), body);
            }
        }
        return (string.Join("\n", lines[1..]), "");
    }

    // Parses a frontmatter block (or a whole .yml manifest) into a FrontmatterBlock.
    public static FrontmatterBlock Parse(string block)
    {
        var lines = Normalize(block).Split('\n');
        var result = new FrontmatterBlock();
        var i = 0;
        while (i < lines.Length)
        {
            if (IsBlankOrComment(lines[i]) || Indent(lines[i]) != 0)
            {
                i++;
                continue;
            }
            i = ParseTopLevel(lines, i, result);
        }
        return result;
    }

    private static int ParseTopLevel(string[] lines, int i, FrontmatterBlock result)
    {
        if (!SplitKey(lines[i], out var key, out var rest))
        {
            return i + 1;
        }
        if (key == "groups" && rest.Length == 0)
        {
            return ParseGroups(lines, i + 1, result);
        }
        if (rest.Length == 0)
        {
            var (items, next) = ParseBlockList(lines, i + 1);
            if (items.Count > 0)
            {
                result.SetList(key, items);
            }
            else
            {
                result.SetScalar(key, "");
            }
            return next;
        }
        if (rest[0] == '[')
        {
            result.SetList(key, ParseInlineList(rest));
            return i + 1;
        }
        result.SetScalar(key, Unquote(rest));
        return i + 1;
    }

    private static (List<string> Items, int Next) ParseBlockList(string[] lines, int start)
    {
        var items = new List<string>();
        var i = start;
        while (i < lines.Length)
        {
            if (IsBlankOrComment(lines[i]))
            {
                i++;
                continue;
            }
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                break;
            }
            var value = Unquote(trimmed[2..].Trim());
            if (value.Length > 0)
            {
                items.Add(value);
            }
            i++;
        }
        return (items, i);
    }

    // A new group begins only at a `- ` dash sitting at the first item's indent.
    // A deeper-indented `- ` belongs to an unsupported block sub-list (group list
    // fields are inline-only per the schema) and is ignored, so it can neither
    // spawn a phantom group nor steal the real group's fields.
    private static int ParseGroups(string[] lines, int start, FrontmatterBlock result)
    {
        FrontmatterBlock? current = null;
        var itemIndent = -1;
        var i = start;
        while (i < lines.Length)
        {
            if (IsBlankOrComment(lines[i]))
            {
                i++;
                continue;
            }
            var indent = Indent(lines[i]);
            if (indent == 0)
            {
                break;
            }
            var trimmed = lines[i].TrimStart();
            var isDash = trimmed.StartsWith("- ", StringComparison.Ordinal);
            if (isDash && (itemIndent < 0 || indent == itemIndent))
            {
                itemIndent = indent;
                current = new FrontmatterBlock();
                result.AddGroup(current);
                ApplyField(current, trimmed[2..]);
            }
            else if (!isDash && current != null)
            {
                ApplyField(current, trimmed);
            }
            i++;
        }
        return i;
    }

    private static void ApplyField(FrontmatterBlock group, string fieldText)
    {
        if (!SplitKey(fieldText, out var key, out var rest))
        {
            return;
        }
        if (rest.Length > 0 && rest[0] == '[')
        {
            group.SetList(key, ParseInlineList(rest));
            return;
        }
        group.SetScalar(key, Unquote(rest));
    }

    private static List<string> ParseInlineList(string rest)
    {
        var inner = rest.Trim();
        if (inner.StartsWith("[", StringComparison.Ordinal))
        {
            inner = inner[1..];
        }
        if (inner.EndsWith("]", StringComparison.Ordinal))
        {
            inner = inner[..^1];
        }
        var items = new List<string>();
        foreach (var part in inner.Split(','))
        {
            var value = Unquote(part.Trim());
            if (value.Length > 0)
            {
                items.Add(value);
            }
        }
        return items;
    }

    private static bool SplitKey(string line, out string key, out string rest)
    {
        key = "";
        rest = "";
        var trimmed = line.TrimStart();
        var colon = trimmed.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }
        var candidate = trimmed[..colon].Trim();
        if (!IsKey(candidate))
        {
            return false;
        }
        key = candidate;
        rest = trimmed[(colon + 1)..].Trim();
        return true;
    }

    private static bool IsKey(string key)
    {
        foreach (var c in key)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
            {
                return false;
            }
        }
        return key.Length > 0;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2)
        {
            var first = value[0];
            var last = value[^1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
            {
                return value[1..^1];
            }
        }
        return value;
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").Replace("\r", "\n");

    private static bool IsBlankOrComment(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length == 0 || trimmed[0] == '#';
    }

    private static int Indent(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c != ' ')
            {
                break;
            }
            count++;
        }
        return count;
    }
}

// The parsed key/value content of one frontmatter block (top level) or one
// `groups` item. Holds scalars, scalar lists, and — at the top level only —
// the ordered `groups` items. Lookups never throw: absent keys yield "" / empty.
public sealed class FrontmatterBlock
{
    private readonly Dictionary<string, string> _scalars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _lists = new(StringComparer.Ordinal);
    private readonly List<FrontmatterBlock> _groups = new();

    internal void SetScalar(string key, string value) => _scalars[key] = value;

    internal void SetList(string key, List<string> value) => _lists[key] = value;

    internal void AddGroup(FrontmatterBlock group) => _groups.Add(group);

    public string Scalar(string key) => _scalars.TryGetValue(key, out var v) ? v : "";

    public bool Flag(string key) =>
        string.Equals(Scalar(key), "true", StringComparison.OrdinalIgnoreCase);

    public int Int(string key) => int.TryParse(Scalar(key), out var n) ? n : 0;

    public IReadOnlyList<string> List(string key) =>
        _lists.TryGetValue(key, out var v) ? v : Array.Empty<string>();

    public IReadOnlyList<FrontmatterBlock> Groups => _groups;
}
