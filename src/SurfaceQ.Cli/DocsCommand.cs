using SurfaceQ.Core;

namespace SurfaceQ.Cli;

// `surfaceq docs`: document every library in a workspace as Markdown.
// Each library's API.md is written next to its ng-package.json. The --output
// value is a path relative to each library directory (default "API.md", or
// "SERVICE_API.md" under --services). Declarations tagged @deprecated are omitted
// unless --include-deprecated-types is passed; the same flag opts the Deprecated
// column into the tables (absent by default). The table of contents is omitted
// unless --contents is passed. All libraries are attempted; the command exits 2 if
// any library failed. With --folder, the workspace is not scanned: the given
// directory (and its subfolders) is documented as a single scan root and one
// Markdown file is written at the folder root.

internal static class DocsCommand
{
    public static int Run(
        string? project,
        string? folder,
        string output,
        bool includeImplementations,
        bool services,
        bool includeDeprecatedTypes,
        bool contents,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error)
    {
        // --folder takes precedence: document just that directory and its
        // subfolders, instead of scanning the workspace for libraries.
        if (!string.IsNullOrWhiteSpace(folder))
        {
            return RunFolder(
                folder!, output, includeImplementations, services, includeDeprecatedTypes, contents,
                info, trace, warn, error);
        }

        var root = string.IsNullOrWhiteSpace(project)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(project);

        var manifests = new WorkspaceLocator().FindLibraries(root);
        if (manifests.Count == 0)
        {
            error($"error: no ng-package.json libraries found under '{root}'");
            return 2;
        }
        trace($"trace: found {manifests.Count} library manifest(s)");

        var renderer = new MarkdownRenderer();
        var failed = false;
        foreach (var manifest in manifests)
        {
            var (library, exit) = DocumentationPipeline.Build(
                manifest, includeImplementations, info, trace, warn, error,
                excludeDeprecated: !includeDeprecatedTypes, serviceContracts: services);
            if (exit != 0 || library == null)
            {
                failed = true;
                continue;
            }
            // --include-deprecated-types also opts the Deprecated column into the tables.
            var markdown = renderer.Render(library, contents, includeDeprecatedColumn: includeDeprecatedTypes);
            if (!WriteDoc(manifest, output, markdown, info, error))
            {
                failed = true;
            }
        }
        return failed ? 2 : 0;
    }

    // Folder mode: treat the directory as a self-contained scan root (walking it and
    // all subfolders), document it, and write one Markdown file at the folder root.
    // The same --services/--include-* filters apply, so a folder holding a single
    // service yields a document with just that one declaration set.
    private static int RunFolder(
        string folder,
        string output,
        bool includeImplementations,
        bool services,
        bool includeDeprecatedTypes,
        bool contents,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error)
    {
        var (library, exit) = DocumentationPipeline.BuildFromFolder(
            folder, includeImplementations, info, trace, warn, error,
            excludeDeprecated: !includeDeprecatedTypes, serviceContracts: services);
        if (exit != 0 || library == null)
        {
            return 2;
        }
        var folderDir = Path.GetFullPath(folder);
        var markdown = new MarkdownRenderer().Render(
            library, contents, includeDeprecatedColumn: includeDeprecatedTypes);
        return WriteDocToDir(folderDir, output, markdown, info, error) ? 0 : 2;
    }

    private static bool WriteDoc(
        string manifest,
        string output,
        string markdown,
        Action<string> info,
        Action<string> error)
    {
        var libDir = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        return WriteDocToDir(libDir, output, markdown, info, error);
    }

    private static bool WriteDocToDir(
        string baseDir,
        string output,
        string markdown,
        Action<string> info,
        Action<string> error)
    {
        var outputPath = Path.GetFullPath(Path.Combine(baseDir, output));
        try
        {
            var outputDir = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(outputPath, markdown);
        }
        catch (UnauthorizedAccessException ex)
        {
            error($"error: cannot write '{outputPath}': {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            error($"error: cannot write '{outputPath}': {ex.Message}");
            return false;
        }
        info($"info: wrote {outputPath}");
        return true;
    }
}
