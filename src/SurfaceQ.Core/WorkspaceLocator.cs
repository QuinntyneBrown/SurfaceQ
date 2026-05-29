namespace SurfaceQ.Core;

// Finds every Angular library in a workspace by its ng-package.json manifest.
// A library is any directory containing ng-package.json; node_modules and
// build output (dist) are skipped. Results are sorted for deterministic output.

public sealed class WorkspaceLocator
{
    private const string ManifestName = "ng-package.json";

    public IReadOnlyList<string> FindLibraries(string workspaceRoot)
    {
        var root = Path.GetFullPath(workspaceRoot);
        if (!Directory.Exists(root))
        {
            return Array.Empty<string>();
        }
        var manifests = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, ManifestName, SearchOption.AllDirectories))
        {
            var full = Path.GetFullPath(path);
            if (IsExcluded(full))
            {
                continue;
            }
            manifests.Add(full);
        }
        return manifests
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsExcluded(string fullPath)
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
