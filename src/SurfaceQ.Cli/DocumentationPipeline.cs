using System.Text.Json;
using SurfaceQ.Core;
using SurfaceQ.Sidecar;

namespace SurfaceQ.Cli;

// Builds the LibraryApi for a single library: read its manifest, walk its
// sources, ask the sidecar to `document` each file, and assemble the result.
// Parallels OutputPipeline but uses the richer `document` method.

internal static class DocumentationPipeline
{
    public static (LibraryApi? Library, int ExitCode) Build(
        string manifestPath,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error)
    {
        ProjectContext context;
        try
        {
            context = new ManifestReader().Read(manifestPath, info);
        }
        catch (ManifestException ex)
        {
            error(ex.Message);
            return (null, 2);
        }

        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var sources = new SourceFileWalker().Walk(context).ToList();
        trace($"trace: walker returned {sources.Count} source file(s) for '{manifestDir}'");

        var declarations = new List<ApiDeclaration>();
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
                method = "document",
                @params = new { file = source },
            });
            using var doc = JsonDocument.Parse(sidecar.Send(request));
            var result = doc.RootElement.GetProperty("result");

            foreach (var d in result.GetProperty("declarations").EnumerateArray())
            {
                declarations.Add(ParseDeclaration(d));
            }
            CollectWarnings(result, manifestDir, warnedFiles, warn);
            CollectErrors(result, manifestDir, errorMessages);
        }

        if (errorMessages.Count > 0)
        {
            foreach (var msg in errorMessages)
            {
                error(msg);
            }
            return (null, 2);
        }
        return (new LibraryApi(LibraryName(manifestPath), declarations), 0);
    }

    private static void CollectWarnings(
        JsonElement result,
        string manifestDir,
        HashSet<string> warnedFiles,
        Action<string> warn)
    {
        foreach (var w in result.GetProperty("warnings").EnumerateArray())
        {
            var normalized = Path.GetFullPath(w.GetProperty("file").GetString()!);
            if (!warnedFiles.Add(normalized))
            {
                continue;
            }
            var rel = Path.GetRelativePath(manifestDir, normalized).Replace('\\', '/');
            warn($"warn: {w.GetProperty("code").GetString()} in '{rel}'");
        }
    }

    private static void CollectErrors(JsonElement result, string manifestDir, List<string> errorMessages)
    {
        foreach (var e in result.GetProperty("errors").EnumerateArray())
        {
            var rel = Path.GetRelativePath(manifestDir, Path.GetFullPath(e.GetProperty("file").GetString()!))
                .Replace('\\', '/');
            var line = e.GetProperty("line").GetInt32();
            var message = e.GetProperty("message").GetString();
            errorMessages.Add($"error: parse error in '{rel}' at line {line}: {message}");
        }
    }

    private static ApiDeclaration ParseDeclaration(JsonElement d)
    {
        return new ApiDeclaration(
            Name: Str(d, "name"),
            Kind: Str(d, "kind"),
            Doc: Str(d, "doc"),
            Definition: Str(d, "definition"),
            Contract: Str(d, "contract"),
            Description: Str(d, "description"),
            Type: Str(d, "type"),
            ReturnType: Str(d, "returnType"),
            Extends: StrArray(d, "extends"),
            Implements: StrArray(d, "implements"),
            Parameters: ParseParameters(d),
            Members: ParseMembers(d),
            EnumMembers: ParseEnumMembers(d));
    }

    private static IReadOnlyList<ApiParameter> ParseParameters(JsonElement parent)
    {
        if (!parent.TryGetProperty("parameters", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ApiParameter>();
        }
        var list = new List<ApiParameter>();
        foreach (var p in arr.EnumerateArray())
        {
            list.Add(new ApiParameter(Str(p, "name"), Str(p, "type"), Bool(p, "optional")));
        }
        return list;
    }

    private static IReadOnlyList<ApiMember> ParseMembers(JsonElement d)
    {
        if (!d.TryGetProperty("members", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ApiMember>();
        }
        var list = new List<ApiMember>();
        foreach (var m in arr.EnumerateArray())
        {
            list.Add(new ApiMember(
                MemberKind: Str(m, "memberKind"),
                Name: Str(m, "name"),
                Type: Str(m, "type"),
                ReturnType: Str(m, "returnType"),
                Optional: Bool(m, "optional"),
                Readonly: Bool(m, "readonly"),
                Parameters: ParseParameters(m),
                Doc: Str(m, "doc")));
        }
        return list;
    }

    private static IReadOnlyList<EnumMember> ParseEnumMembers(JsonElement d)
    {
        if (!d.TryGetProperty("members", out var arr) || d.GetProperty("kind").GetString() != "enum")
        {
            return Array.Empty<EnumMember>();
        }
        var list = new List<EnumMember>();
        foreach (var m in arr.EnumerateArray())
        {
            list.Add(new EnumMember(Str(m, "name"), Str(m, "value"), Str(m, "doc")));
        }
        return list;
    }

    private static string LibraryName(string manifestPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var packageJson = Path.Combine(dir, "package.json");
        if (File.Exists(packageJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(packageJson));
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("name", out var name)
                    && name.ValueKind == JsonValueKind.String)
                {
                    var value = name.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value!;
                    }
                }
            }
            catch (JsonException)
            {
            }
        }
        return new DirectoryInfo(dir).Name;
    }

    private static string Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()! : "";

    private static bool Bool(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;

    private static IReadOnlyList<string> StrArray(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString()!);
            }
        }
        return list;
    }
}
