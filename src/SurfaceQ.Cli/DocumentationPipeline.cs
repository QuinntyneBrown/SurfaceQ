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
        bool includeImplementations,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error,
        bool excludeDeprecated = false,
        bool serviceContracts = false)
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
        return BuildFromContext(
            context, manifestDir, LibraryName(manifestPath), includeImplementations,
            info, trace, warn, error, excludeDeprecated, serviceContracts);
    }

    // Folder mode: treat an arbitrary directory as a self-contained scan root, with
    // no ng-package.json and no entry file to exclude. Used by the providers and docs
    // commands when invoked with --folder. The library name is just the folder's own
    // name. The docs filters default off, so the providers caller is unaffected.
    public static (LibraryApi? Library, int ExitCode) BuildFromFolder(
        string folderPath,
        bool includeImplementations,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error,
        bool excludeDeprecated = false,
        bool serviceContracts = false)
    {
        var root = Path.GetFullPath(folderPath);
        if (!Directory.Exists(root))
        {
            error($"error: folder does not exist: '{root}'");
            return (null, 2);
        }
        // No entry file in folder mode; an empty EntryFile excludes nothing.
        var context = new ProjectContext(root, "", root);
        return BuildFromContext(
            context, root, new DirectoryInfo(root).Name, includeImplementations,
            info, trace, warn, error, excludeDeprecated, serviceContracts);
    }

    private static (LibraryApi? Library, int ExitCode) BuildFromContext(
        ProjectContext context,
        string baseDir,
        string libraryName,
        bool includeImplementations,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error,
        bool excludeDeprecated = false,
        bool serviceContracts = false)
    {
        var sources = new SourceFileWalker().Walk(context).ToList();
        trace($"trace: walker returned {sources.Count} source file(s) for '{baseDir}'");

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
            CollectWarnings(result, baseDir, warnedFiles, warn);
            CollectErrors(result, baseDir, errorMessages);
        }

        if (errorMessages.Count > 0)
        {
            foreach (var msg in errorMessages)
            {
                error(msg);
            }
            return (null, 2);
        }
        var documented = SelectDeclarations(
            declarations, includeImplementations, serviceContracts, excludeDeprecated, info);
        return (new LibraryApi(libraryName, documented), 0);
    }

    // Applies the docs command's post-extraction filters. Services mode documents
    // the service *contract* surface — the interfaces an @Injectable implements,
    // the models/types/enums those contracts are built from, and the injection
    // tokens that wire them — and drops the concrete classes; it therefore
    // supersedes implementation-hiding rather than composing with it. Deprecated-
    // declaration exclusion then composes on top of whichever set was selected. Both
    // filters default off, so the providers caller sees every extracted declaration.
    private static List<ApiDeclaration> SelectDeclarations(
        List<ApiDeclaration> declarations,
        bool includeImplementations,
        bool serviceContracts,
        bool excludeDeprecated,
        Action<string> info)
    {
        var kept = serviceContracts
            ? OnlyServiceContracts(declarations, info)
            : includeImplementations ? declarations : ExcludeImplementations(declarations, info);
        return excludeDeprecated ? ExcludeDeprecated(kept, info) : kept;
    }

    // Services mode documents what a consumer of the services codes against: the
    // interfaces an @Injectable implements, the models/types/enums those contracts
    // are built from, and the injection tokens that wire them — not the concrete
    // service classes. Every class (the services and any other implementation) is
    // reached through a token, so classes, functions, and plain consts drop out,
    // leaving the pure contract surface.
    private static List<ApiDeclaration> OnlyServiceContracts(
        List<ApiDeclaration> declarations,
        Action<string> info)
    {
        var kept = new List<ApiDeclaration>();
        foreach (var d in declarations)
        {
            if (IsContractKind(d.Kind))
            {
                kept.Add(d);
            }
        }
        info($"info: services mode: documenting {kept.Count} contract declaration(s) " +
             "(interfaces, types, enums, injection tokens); service classes are excluded");
        return kept;
    }

    // The kinds that make up a service contract surface — the data shapes a consumer
    // codes against. Classes (services and other implementations), functions, and
    // plain consts are implementations, not contract, and are left out.
    private static bool IsContractKind(string kind) =>
        kind is "interface" or "type" or "enum" or "injection-token";

    // Drops every declaration carrying an @deprecated JSDoc tag (the whole type).
    // Member-level deprecations inside a kept declaration are untouched — they are
    // still rendered with their Deprecated column. --include-deprecated-types skips this.
    private static List<ApiDeclaration> ExcludeDeprecated(
        List<ApiDeclaration> declarations,
        Action<string> info)
    {
        var kept = new List<ApiDeclaration>();
        var hidden = new List<string>();
        foreach (var d in declarations)
        {
            if (d.Deprecated)
            {
                hidden.Add(d.Name);
            }
            else
            {
                kept.Add(d);
            }
        }
        if (hidden.Count > 0)
        {
            hidden.Sort(StringComparer.Ordinal);
            info($"info: excluded {hidden.Count} deprecated declaration(s) " +
                 $"({string.Join(", ", hidden)}); pass --include-deprecated-types to show them");
        }
        return kept;
    }

    // By default the document represents the contract a consumer codes against:
    // exported interfaces + injection tokens. A class that implements an
    // interface exported by the same library is an implementation detail reached
    // via its token, so it is hidden unless --include-implementations is passed.
    // Classes that implement only external interfaces (e.g. ControlValueAccessor)
    // or no interface are used directly and stay visible.
    private static List<ApiDeclaration> ExcludeImplementations(
        List<ApiDeclaration> declarations,
        Action<string> info)
    {
        var exportedInterfaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in declarations)
        {
            if (d.Kind == "interface")
            {
                exportedInterfaces.Add(d.Name);
            }
        }

        var kept = new List<ApiDeclaration>();
        var hidden = new List<string>();
        foreach (var d in declarations)
        {
            if (d.Kind == "class" && ImplementsExportedInterface(d, exportedInterfaces))
            {
                hidden.Add(d.Name);
            }
            else
            {
                kept.Add(d);
            }
        }

        if (hidden.Count > 0)
        {
            hidden.Sort(StringComparer.Ordinal);
            info($"info: hid {hidden.Count} implementation class(es) behind exported interfaces " +
                 $"({string.Join(", ", hidden)}); pass --include-implementations to show them");
        }
        return kept;
    }

    private static bool ImplementsExportedInterface(ApiDeclaration cls, HashSet<string> exportedInterfaces)
    {
        foreach (var implemented in cls.Implements)
        {
            if (exportedInterfaces.Contains(BaseTypeName(implemented)))
            {
                return true;
            }
        }
        return false;
    }

    // "ns.IFoo<Bar>" -> "IFoo": drop type arguments, then any namespace qualifier.
    private static string BaseTypeName(string heritage)
    {
        var name = heritage;
        var generic = name.IndexOf('<');
        if (generic >= 0)
        {
            name = name.Substring(0, generic);
        }
        var dot = name.LastIndexOf('.');
        if (dot >= 0)
        {
            name = name.Substring(dot + 1);
        }
        return name.Trim();
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
            EnumMembers: ParseEnumMembers(d),
            Deprecated: Bool(d, "deprecated"),
            DeprecationReason: Str(d, "deprecationReason"),
            File: Str(d, "file"),
            Role: Str(d, "role"));
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
                Doc: Str(m, "doc"),
                Deprecated: Bool(m, "deprecated"),
                DeprecationReason: Str(m, "deprecationReason")));
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
            list.Add(new EnumMember(
                Str(m, "name"),
                Str(m, "value"),
                Str(m, "doc"),
                Bool(m, "deprecated"),
                Str(m, "deprecationReason")));
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
