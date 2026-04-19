namespace SurfaceQ.Core;

public sealed class SourceFileWalker
{
    public IEnumerable<string> Walk(ProjectContext context)
    {
        var entryFull = Path.GetFullPath(context.EntryFile);
        foreach (var path in Directory.EnumerateFiles(context.ScanRoot, "*.ts", SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(path);
            if (string.Equals(full, entryFull, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (ContainsNodeModulesSegment(full))
            {
                continue;
            }
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

    private static bool ContainsNodeModulesSegment(string fullPath)
    {
        var segments = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var segment in segments)
        {
            if (segment == "node_modules")
            {
                return true;
            }
        }
        return false;
    }
}
