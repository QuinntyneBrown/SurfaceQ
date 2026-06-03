using System.Text.Json;
using SurfaceQ.Core;
using SurfaceQ.Sidecar;

namespace SurfaceQ.Cli;

// Builds the ProjectInventory for one project: walk its sources, ask the sidecar
// to `inventory` each file, and assemble the items with project-relative POSIX
// paths. Parallels DocumentationPipeline but uses the richer `inventory` method
// that reports every declaration, exported or not.

internal static class InventoryPipeline
{
    public static (ProjectInventory? Inventory, int ExitCode) Build(
        InventoryProject project,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error)
    {
        var sources = new InventoryWalker().Walk(project.SourceRoot);
        trace($"trace: walker returned {sources.Count} source file(s) for '{project.Name}'");

        var items = new List<InventoryItem>();
        var errorMessages = new List<string>();
        var warnedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var scriptPath = OutputPipeline.ResolveSidecarScript();
        using var sidecar = new SidecarClient(scriptPath);
        var id = 0;
        foreach (var source in sources)
        {
            id++;
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method = "inventory",
                @params = new { file = source },
            });
            using var doc = JsonDocument.Parse(sidecar.Send(request));
            var result = doc.RootElement.GetProperty("result");

            var relative = Relativize(project.ProjectDir, source);
            foreach (var item in result.GetProperty("items").EnumerateArray())
            {
                items.Add(ParseItem(item, relative));
            }
            CollectWarnings(result, project.ProjectDir, warnedFiles, warn);
            CollectErrors(result, project.ProjectDir, errorMessages);
        }

        if (errorMessages.Count > 0)
        {
            foreach (var msg in errorMessages)
            {
                error(msg);
            }
            return (null, 2);
        }
        return (new ProjectInventory(project.Name, project.Type, items), 0);
    }

    private static InventoryItem ParseItem(JsonElement item, string relativeFile)
    {
        return new InventoryItem(
            Name: Str(item, "name"),
            Kind: Str(item, "kind"),
            Role: Str(item, "role"),
            Exported: Bool(item, "exported"),
            File: relativeFile,
            Doc: Str(item, "doc"),
            Deprecated: Bool(item, "deprecated"),
            DeprecationReason: Str(item, "deprecationReason"));
    }

    private static string Relativize(string projectDir, string file) =>
        Path.GetRelativePath(projectDir, file).Replace('\\', '/');

    private static void CollectWarnings(
        JsonElement result, string projectDir, HashSet<string> warnedFiles, Action<string> warn)
    {
        foreach (var w in result.GetProperty("warnings").EnumerateArray())
        {
            var normalized = Path.GetFullPath(w.GetProperty("file").GetString()!);
            if (!warnedFiles.Add(normalized))
            {
                continue;
            }
            var rel = Path.GetRelativePath(projectDir, normalized).Replace('\\', '/');
            warn($"warn: {w.GetProperty("code").GetString()} in '{rel}'");
        }
    }

    private static void CollectErrors(JsonElement result, string projectDir, List<string> errorMessages)
    {
        foreach (var e in result.GetProperty("errors").EnumerateArray())
        {
            var rel = Path.GetRelativePath(projectDir, Path.GetFullPath(e.GetProperty("file").GetString()!))
                .Replace('\\', '/');
            var line = e.GetProperty("line").GetInt32();
            var message = e.GetProperty("message").GetString();
            errorMessages.Add($"error: parse error in '{rel}' at line {line}: {message}");
        }
    }

    private static string Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";

    private static bool Bool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
}
