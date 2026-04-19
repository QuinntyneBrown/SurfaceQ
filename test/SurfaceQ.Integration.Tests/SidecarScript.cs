namespace SurfaceQ.Integration.Tests;

internal static class SidecarScript
{
    public static string ResolvePath()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(SidecarScript).Assembly.Location)!);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SurfaceQ.Sidecar.Node", "sidecar.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("sidecar.js not found by walking upward from test assembly location");
    }
}
