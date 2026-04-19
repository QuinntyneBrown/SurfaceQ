using System.Text.Json;

namespace SurfaceQ.Core;

public sealed class ManifestReader
{
    private const string DefaultEntryFile = "src/public-api.ts";

    public ProjectContext Read(string manifestPath)
    {
        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var json = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(json);

        var entryRelative = DefaultEntryFile;
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("entryFile", out var prop)
            && prop.ValueKind == JsonValueKind.String)
        {
            entryRelative = prop.GetString() ?? DefaultEntryFile;
        }

        var entryFile = Path.GetFullPath(Path.Combine(manifestDir, entryRelative));
        var scanRoot = Path.GetDirectoryName(entryFile)!;
        return new ProjectContext(manifestPath, entryFile, scanRoot);
    }
}
