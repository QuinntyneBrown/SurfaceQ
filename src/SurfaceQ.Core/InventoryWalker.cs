namespace SurfaceQ.Core;

// Enumerates the `.ts` files to inventory under a project's source root.
// Excludes tests and stories (the command documents production objects, not
// tests), ambient `.d.ts` typings, and anything under node_modules or dist.
// Barrels (index.ts, public-api.ts) are kept but contribute nothing because
// the sidecar reports only real declarations, never re-exports. Results are
// sorted by Ordinal relative path so output is deterministic and cross-platform.

public sealed class InventoryWalker
{
    public IReadOnlyList<string> Walk(string sourceRoot)
    {
        var root = Path.GetFullPath(sourceRoot);
        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }
        var kept = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.ts", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            // The "*.ts" glob matches case-insensitively on Windows/macOS but not
            // on Linux; an Ordinal check keeps the file set identical across OSes.
            if (!name.EndsWith(".ts", StringComparison.Ordinal))
            {
                continue;
            }
            var full = Path.GetFullPath(path);
            if (IsExcludedDirectory(full) || IsExcludedFile(name))
            {
                continue;
            }
            kept.Add(full);
        }
        return kept
            .OrderBy(p => Path.GetRelativePath(root, p), StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsExcludedFile(string name)
    {
        return name.EndsWith(".spec.ts", StringComparison.Ordinal)
            || name.EndsWith(".stories.ts", StringComparison.Ordinal)
            || name.EndsWith(".e2e-spec.ts", StringComparison.Ordinal)
            || name.EndsWith(".e2e.ts", StringComparison.Ordinal)
            || name.EndsWith(".d.ts", StringComparison.Ordinal);
    }

    private static bool IsExcludedDirectory(string fullPath)
    {
        var segments = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var segment in segments)
        {
            if (segment == "node_modules" || segment == "dist")
            {
                return true;
            }
        }
        return false;
    }
}
