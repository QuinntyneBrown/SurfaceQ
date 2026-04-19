namespace SurfaceQ.Core;

public sealed class ProjectLocator
{
    private const string ManifestName = "ng-package.json";

    public string? Locate(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ManifestName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
