namespace SurfaceQ.Core;

public sealed class SourceFileWalker
{
    public IEnumerable<string> Walk(ProjectContext context)
    {
        foreach (var path in Directory.EnumerateFiles(context.ScanRoot, "*.ts", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith(".spec.ts", StringComparison.Ordinal))
            {
                continue;
            }
            if (name.EndsWith(".stories.ts", StringComparison.Ordinal))
            {
                continue;
            }
            if (name == "index.ts")
            {
                continue;
            }
            yield return path;
        }
    }
}
