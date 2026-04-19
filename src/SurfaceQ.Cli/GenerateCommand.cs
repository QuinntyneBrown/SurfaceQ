using System.Text.Json;
using SurfaceQ.Core;
using SurfaceQ.Sidecar;

namespace SurfaceQ.Cli;

internal static class GenerateCommand
{
    public static int Run(string? project, Action<string> info, Action<string> error)
    {
        var startPath = ResolveStartPath(project);
        var manifest = new ProjectLocator().Locate(startPath);
        if (manifest == null)
        {
            error($"error: could not find ng-package.json searching from '{startPath}'");
            return 2;
        }
        ProjectContext context;
        try
        {
            context = new ManifestReader().Read(manifest, info);
        }
        catch (ManifestException ex)
        {
            error(ex.Message);
            return 2;
        }

        var sources = new SourceFileWalker().Walk(context).ToList();
        var files = DiscoverAllExports(sources);
        var output = new PublicApiRenderer().Render(files, context);
        File.WriteAllText(context.EntryFile, output);
        info($"info: wrote {context.EntryFile}");
        return 0;
    }

    private static List<FileExports> DiscoverAllExports(List<string> sources)
    {
        var grouped = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);

        using var sidecar = new SidecarClient(ResolveSidecarScript());
        var id = 0;
        foreach (var source in sources)
        {
            id++;
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                method = "discover",
                @params = new { file = source },
            });
            var response = sidecar.Send(request);
            using var doc = JsonDocument.Parse(response);
            foreach (var entry in doc.RootElement.GetProperty("result").EnumerateArray())
            {
                var name = entry.GetProperty("name").GetString()!;
                var isType = entry.GetProperty("isType").GetBoolean();
                var declFile = entry.GetProperty("file").GetString()!;
                var normalized = Path.GetFullPath(declFile);
                if (!grouped.TryGetValue(normalized, out var names))
                {
                    names = new Dictionary<string, bool>(StringComparer.Ordinal);
                    grouped[normalized] = names;
                }
                if (!names.ContainsKey(name))
                {
                    names[name] = isType;
                }
            }
        }

        var result = new List<FileExports>();
        foreach (var kv in grouped.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var valueNames = kv.Value.Where(n => !n.Value).Select(n => n.Key).ToList();
            var typeNames = kv.Value.Where(n => n.Value).Select(n => n.Key).ToList();
            if (valueNames.Count == 0 && typeNames.Count == 0)
            {
                continue;
            }
            result.Add(new FileExports(kv.Key, valueNames, typeNames));
        }
        return result;
    }

    private static string ResolveSidecarScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SurfaceQ.Sidecar.Node", "sidecar.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("sidecar.js not found; walked upward from " + AppContext.BaseDirectory);
    }

    private static string ResolveStartPath(string? project)
    {
        if (string.IsNullOrWhiteSpace(project))
        {
            return Directory.GetCurrentDirectory();
        }
        if (File.Exists(project))
        {
            return Path.GetDirectoryName(Path.GetFullPath(project))!;
        }
        return Path.GetFullPath(project);
    }
}
