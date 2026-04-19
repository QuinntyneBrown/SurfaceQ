using System.Text;

namespace SurfaceQ.Cli;

internal static class UnifiedDiff
{
    public static string Format(string expected, string actual, string label)
    {
        var expectedLines = SplitLines(expected);
        var actualLines = SplitLines(actual);
        var sb = new StringBuilder();
        sb.Append("--- ");
        sb.Append(label);
        sb.Append(" (expected)\n");
        sb.Append("+++ ");
        sb.Append(label);
        sb.Append(" (actual)\n");
        sb.Append("@@ -1,");
        sb.Append(expectedLines.Count);
        sb.Append(" +1,");
        sb.Append(actualLines.Count);
        sb.Append(" @@\n");
        foreach (var line in DiffLines(expectedLines, actualLines))
        {
            sb.Append(line);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }
        var list = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                list.Add(text.Substring(start, i - start));
                start = i + 1;
            }
        }
        if (start < text.Length)
        {
            list.Add(text.Substring(start));
        }
        return list;
    }

    private static List<string> DiffLines(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var n = a.Count;
        var m = b.Count;
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                if (string.Equals(a[i], b[j], StringComparison.Ordinal))
                {
                    lcs[i, j] = lcs[i + 1, j + 1] + 1;
                }
                else
                {
                    lcs[i, j] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }
        }
        var result = new List<string>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (string.Equals(a[x], b[y], StringComparison.Ordinal))
            {
                result.Add(" " + a[x]);
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                result.Add("-" + a[x]);
                x++;
            }
            else
            {
                result.Add("+" + b[y]);
                y++;
            }
        }
        while (x < n)
        {
            result.Add("-" + a[x]);
            x++;
        }
        while (y < m)
        {
            result.Add("+" + b[y]);
            y++;
        }
        return result;
    }
}
