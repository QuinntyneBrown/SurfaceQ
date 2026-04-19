namespace SurfaceQ.Core;

public sealed record FileExports(string RelativeModulePath);

public sealed class PublicApiRenderer
{
    private const string Header =
        "// ============================================================\n" +
        "// SurfaceQ — generated public API. DO NOT EDIT.\n" +
        "// Regenerate with `surfaceq generate`.\n" +
        "// ============================================================\n";

    public string Render(IReadOnlyList<FileExports> files, ProjectContext context)
    {
        return Header;
    }
}
