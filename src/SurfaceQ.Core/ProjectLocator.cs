namespace SurfaceQ.Core;

public sealed class ProjectLocator
{
    private const string ManifestName = "ng-package.json";

    public string? Locate(string startPath)
    {
        var candidate = Path.Combine(startPath, ManifestName);
        if (File.Exists(candidate))
        {
            return candidate;
        }
        return null;
    }
}
