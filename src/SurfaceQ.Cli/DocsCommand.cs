using SurfaceQ.Core;

namespace SurfaceQ.Cli;

// `surfaceq docs`: document every library in a workspace as Markdown.
// Each library's API.md is written next to its ng-package.json. The --output
// value is a path relative to each library directory (default "API.md").
// All libraries are attempted; the command exits 2 if any library failed.

internal static class DocsCommand
{
    public static int Run(
        string? project,
        string output,
        bool includeImplementations,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error)
    {
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
                manifest, includeImplementations, info, trace, warn, error);
            if (exit != 0 || library == null)
            {
                failed = true;
                continue;
            }
            if (!WriteDoc(manifest, output, renderer.Render(library), info, error))
            {
                failed = true;
            }
        }
        return failed ? 2 : 0;
    }

    private static bool WriteDoc(
        string manifest,
        string output,
        string markdown,
        Action<string> info,
        Action<string> error)
    {
        var libDir = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        var outputPath = Path.GetFullPath(Path.Combine(libDir, output));
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
