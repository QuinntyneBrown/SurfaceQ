using System.Text;

namespace SurfaceQ.Core;

public sealed record FileExports(
    string SourceFile,
    IReadOnlyList<string> ValueNames,
    IReadOnlyList<string> TypeNames);

public sealed class PublicApiRenderer
{
    private const string Header =
        "// ============================================================\n" +
        "// SurfaceQ — generated public API. DO NOT EDIT.\n" +
        "// Regenerate with `surfaceq generate`.\n" +
        "// ============================================================\n";

    public string Render(IReadOnlyList<FileExports> files, ProjectContext context)
    {
        var entryDir = Path.GetDirectoryName(context.EntryFile)!;
        var sb = new StringBuilder();
        sb.Append(Header);
        foreach (var file in files)
        {
            var specifier = ComputeSpecifier(file.SourceFile, entryDir);
            if (file.ValueNames.Count > 0)
            {
                sb.Append("export { ");
                sb.Append(string.Join(", ", file.ValueNames));
                sb.Append(" } from '");
                sb.Append(specifier);
                sb.Append("';\n");
            }
            if (file.TypeNames.Count > 0)
            {
                sb.Append("export type { ");
                sb.Append(string.Join(", ", file.TypeNames));
                sb.Append(" } from '");
                sb.Append(specifier);
                sb.Append("';\n");
            }
        }
        return sb.ToString();
    }

    private static string ComputeSpecifier(string sourceFile, string entryDir)
    {
        var rel = Path.GetRelativePath(entryDir, sourceFile);
        if (rel.EndsWith(".ts", StringComparison.Ordinal))
        {
            rel = rel.Substring(0, rel.Length - 3);
        }
        rel = rel.Replace('\\', '/');
        if (!rel.StartsWith("./", StringComparison.Ordinal) && !rel.StartsWith("../", StringComparison.Ordinal))
        {
            rel = "./" + rel;
        }
        return rel;
    }
}
