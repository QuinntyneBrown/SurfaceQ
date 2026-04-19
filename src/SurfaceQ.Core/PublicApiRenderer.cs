using System.Text;

namespace SurfaceQ.Core;

public sealed record FileExports(
    string RelativeModulePath,
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
        var sb = new StringBuilder();
        sb.Append(Header);
        foreach (var file in files)
        {
            if (file.ValueNames.Count > 0)
            {
                sb.Append("export { ");
                sb.Append(string.Join(", ", file.ValueNames));
                sb.Append(" } from '");
                sb.Append(file.RelativeModulePath);
                sb.Append("';\n");
            }
            if (file.TypeNames.Count > 0)
            {
                sb.Append("export type { ");
                sb.Append(string.Join(", ", file.TypeNames));
                sb.Append(" } from '");
                sb.Append(file.RelativeModulePath);
                sb.Append("';\n");
            }
        }
        return sb.ToString();
    }
}
