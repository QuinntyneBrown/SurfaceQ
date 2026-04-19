using System.Text.Json;
using SurfaceQ.Core;
using SurfaceQ.Sidecar;

namespace SurfaceQ.Cli;

internal static class OutputPipeline
{
    public static PipelineResult Build(string? project, Action<string> info, Action<string> warn, Action<string> error)
    {
        return Build(project, info, _ => { }, warn, error);
    }

    public static PipelineResult Build(string? project, Action<string> info, Action<string> trace, Action<string> warn, Action<string> error)
    {
        var startPath = ResolveStartPath(project);
        var manifest = new ProjectLocator().Locate(startPath);
        if (manifest == null)
        {
            error($"error: could not find ng-package.json searching from '{startPath}'");
            return PipelineResult.Fail(2);
        }
        ProjectContext context;
        try
        {
            context = new ManifestReader().Read(manifest, info);
        }
        catch (ManifestException ex)
        {
            error(ex.Message);
            return PipelineResult.Fail(2);
        }

        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(context.ManifestPath))!;
        var sources = new SourceFileWalker().Walk(context).ToList();
        trace($"trace: walker returned {sources.Count} source file(s)");
        var (files, errors) = DiscoverAllExports(sources, manifestDir, trace, warn);
        if (errors.Count > 0)
        {
            foreach (var msg in errors)
            {
                error(msg);
            }
            return PipelineResult.Fail(2);
        }
        var output = new PublicApiRenderer().Render(files, context);
        return PipelineResult.Ok(context, output);
    }

    public static string ResolveStartPath(string? project)
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

    private static (List<FileExports> Files, List<string> Errors) DiscoverAllExports(
        List<string> sources,
        string manifestDir,
        Action<string> trace,
        Action<string> warn)
    {
        var grouped = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
        var warnedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errorMessages = new List<string>();

        var scriptPath = ResolveSidecarScript();
        trace($"trace: spawning sidecar from '{scriptPath}'");
        using var sidecar = new SidecarClient(scriptPath);
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
            var result = doc.RootElement.GetProperty("result");

            foreach (var entry in result.GetProperty("exports").EnumerateArray())
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

            foreach (var w in result.GetProperty("warnings").EnumerateArray())
            {
                var code = w.GetProperty("code").GetString()!;
                var path = w.GetProperty("file").GetString()!;
                var normalized = Path.GetFullPath(path);
                if (!warnedFiles.Add(normalized))
                {
                    continue;
                }
                var rel = Path.GetRelativePath(manifestDir, normalized).Replace('\\', '/');
                warn($"warn: {code} in '{rel}'");
            }

            foreach (var e in result.GetProperty("errors").EnumerateArray())
            {
                var path = e.GetProperty("file").GetString()!;
                var line = e.GetProperty("line").GetInt32();
                var message = e.GetProperty("message").GetString()!;
                var rel = Path.GetRelativePath(manifestDir, Path.GetFullPath(path)).Replace('\\', '/');
                errorMessages.Add($"error: parse error in '{rel}' at line {line}: {message}");
            }
        }

        var fileExportsList = new List<FileExports>();
        foreach (var kv in grouped.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var valueNames = kv.Value.Where(n => !n.Value).Select(n => n.Key).ToList();
            var typeNames = kv.Value.Where(n => n.Value).Select(n => n.Key).ToList();
            if (valueNames.Count == 0 && typeNames.Count == 0)
            {
                continue;
            }
            fileExportsList.Add(new FileExports(kv.Key, valueNames, typeNames));
        }
        return (fileExportsList, errorMessages);
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
}

internal readonly record struct PipelineResult(int ExitCode, ProjectContext? Context, string? Output)
{
    public static PipelineResult Ok(ProjectContext context, string output) => new(0, context, output);
    public static PipelineResult Fail(int exitCode) => new(exitCode, null, null);
}
