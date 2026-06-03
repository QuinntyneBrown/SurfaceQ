using System.Text.Json;

namespace SurfaceQ.Core;

// Discovers every Angular application and library under a target path so the
// `inventory` command can document each. Applications come from angular.json
// (Angular CLI) and project.json (Nx); libraries from ng-package.json. When
// nothing matches, the target itself is treated as one application. node_modules,
// dist, and *-e2e projects are skipped; results are deduplicated by project
// directory and ordered by name so the set is deterministic across platforms.

public sealed class WorkspaceProjectLocator
{
    private const string DefaultEntryFile = "src/public-api.ts";

    public IReadOnlyList<InventoryProject> Find(string target)
    {
        var root = Path.GetFullPath(target);
        if (!Directory.Exists(root))
        {
            return Array.Empty<InventoryProject>();
        }

        var found = new Dictionary<string, InventoryProject>(StringComparer.OrdinalIgnoreCase);
        FromAngularJson(root, found);
        FromProjectJson(root, found);
        FromNgPackage(root, found);
        if (found.Count == 0)
        {
            Fallback(root, found);
        }

        return found.Values
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => p.ProjectDir, StringComparer.Ordinal)
            .ToList();
    }

    private static void FromAngularJson(string root, Dictionary<string, InventoryProject> found)
    {
        foreach (var manifest in FindManifests(root, "angular.json"))
        {
            var dir = Path.GetDirectoryName(manifest)!;
            using var doc = ReadJson(manifest);
            if (doc is null
                || doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("projects", out var projects)
                || projects.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            foreach (var project in projects.EnumerateObject())
            {
                AddAngularProject(found, dir, project.Name, project.Value);
            }
        }
    }

    private static void AddAngularProject(
        Dictionary<string, InventoryProject> found, string dir, string name, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            // Nx-style index: the value is a relative path to the project directory.
            AddProjectDir(found, name, Path.GetFullPath(Path.Combine(dir, value.GetString()!)), dir);
            return;
        }
        if (value.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        var projectDir = Path.GetFullPath(Path.Combine(dir, Str(value, "root") ?? ""));
        var sourceRootRel = Str(value, "sourceRoot");
        var sourceRoot = sourceRootRel != null
            ? Path.GetFullPath(Path.Combine(dir, sourceRootRel))
            : Path.Combine(projectDir, "src");
        Add(found, name, NormalizeType(Str(value, "projectType"), projectDir), projectDir, sourceRoot);
    }

    private static void FromProjectJson(string root, Dictionary<string, InventoryProject> found)
    {
        foreach (var manifest in FindManifests(root, "project.json"))
        {
            var dir = Path.GetDirectoryName(manifest)!;
            AddProjectDir(found, new DirectoryInfo(dir).Name, dir, root);
        }
    }

    // Reads a project.json in projectDir (if present) for its name, type, and
    // source root. sourceRootBase is the workspace root that an Nx sourceRoot is
    // relative to (the angular.json directory, or the Find() target).
    private static void AddProjectDir(
        Dictionary<string, InventoryProject> found, string fallbackName, string projectDir, string sourceRootBase)
    {
        var name = fallbackName;
        string? type = null;
        string? sourceRootRel = null;
        var manifest = Path.Combine(projectDir, "project.json");
        if (File.Exists(manifest))
        {
            using var doc = ReadJson(manifest);
            if (doc is not null && doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                name = Str(doc.RootElement, "name") ?? fallbackName;
                type = Str(doc.RootElement, "projectType");
                sourceRootRel = Str(doc.RootElement, "sourceRoot");
            }
        }
        Add(found, name, NormalizeType(type, projectDir), projectDir,
            ResolveProjectSourceRoot(sourceRootRel, sourceRootBase, projectDir));
    }

    // An Nx project.json sourceRoot is workspace-root-relative; resolve it there
    // and fall back to <projectDir>/src when absent or pointing at a missing dir.
    private static string ResolveProjectSourceRoot(string? sourceRootRel, string sourceRootBase, string projectDir)
    {
        if (sourceRootRel != null)
        {
            var resolved = Path.GetFullPath(Path.Combine(sourceRootBase, sourceRootRel));
            if (Directory.Exists(resolved))
            {
                return resolved;
            }
        }
        return Path.Combine(projectDir, "src");
    }

    private static void FromNgPackage(string root, Dictionary<string, InventoryProject> found)
    {
        foreach (var manifest in FindManifests(root, "ng-package.json"))
        {
            var libDir = Path.GetDirectoryName(manifest)!;
            Add(found, LibraryName(libDir), "library", libDir, LibrarySourceRoot(manifest, libDir));
        }
    }

    private static void Fallback(string root, Dictionary<string, InventoryProject> found)
    {
        var src = Path.Combine(root, "src");
        var sourceRoot = Directory.Exists(src) ? src : root;
        Add(found, new DirectoryInfo(root).Name, "application", root, sourceRoot);
    }

    private static void Add(
        Dictionary<string, InventoryProject> found, string name, string type, string projectDir, string sourceRoot)
    {
        var key = Path.GetFullPath(projectDir);
        if (found.ContainsKey(key)
            || IsExcluded(key)
            || name.EndsWith("-e2e", StringComparison.Ordinal)
            || !Directory.Exists(sourceRoot))
        {
            return;
        }
        found[key] = new InventoryProject(name, type, key, Path.GetFullPath(sourceRoot));
    }

    private static IEnumerable<string> FindManifests(string root, string fileName)
    {
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .Where(p => !IsExcluded(p))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    private static string LibrarySourceRoot(string manifest, string libDir)
    {
        var entry = DefaultEntryFile;
        using (var doc = ReadJson(manifest))
        {
            if (doc is not null && doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                entry = Str(doc.RootElement, "entryFile") ?? DefaultEntryFile;
            }
        }
        var entryDir = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(libDir, entry)))!;
        if (Directory.Exists(entryDir))
        {
            return entryDir;
        }
        var src = Path.Combine(libDir, "src");
        return Directory.Exists(src) ? src : libDir;
    }

    private static string LibraryName(string libDir)
    {
        var packageJson = Path.Combine(libDir, "package.json");
        if (File.Exists(packageJson))
        {
            using var doc = ReadJson(packageJson);
            if (doc is not null && doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                var name = Str(doc.RootElement, "name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name!;
                }
            }
        }
        return new DirectoryInfo(libDir).Name;
    }

    private static string NormalizeType(string? declared, string projectDir)
    {
        if (declared == "application" || declared == "library")
        {
            return declared;
        }
        return File.Exists(Path.Combine(projectDir, "ng-package.json")) ? "library" : "application";
    }

    private static bool IsExcluded(string fullPath)
    {
        var segments = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var segment in segments)
        {
            if (segment == "node_modules" || segment == "dist")
            {
                return true;
            }
        }
        return false;
    }

    private static JsonDocument? ReadJson(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var prop)
            && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }
}
