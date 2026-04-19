using System.Text.Json;

namespace SurfaceQ.Core;

public sealed class ManifestReader
{
    private const string DefaultEntryFile = "src/public-api.ts";

    public ProjectContext Read(string manifestPath, Action<string>? onInfo = null)
    {
        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var json = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(json);

        var entryRelative = TryReadEntryFile(doc);
        if (entryRelative == null)
        {
            onInfo?.Invoke($"info: ng-package.json has no entryFile; using default '{DefaultEntryFile}'");
            entryRelative = DefaultEntryFile;
        }

        var entryFile = Path.GetFullPath(Path.Combine(manifestDir, entryRelative));
        var scanRoot = Path.GetDirectoryName(entryFile)!;
        return new ProjectContext(manifestPath, entryFile, scanRoot);
    }

    private static string? TryReadEntryFile(JsonDocument doc)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!doc.RootElement.TryGetProperty("entryFile", out var prop))
        {
            return null;
        }
        if (prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return prop.GetString();
    }
}
