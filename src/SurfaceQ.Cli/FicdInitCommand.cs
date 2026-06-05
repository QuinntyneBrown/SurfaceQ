using SurfaceQ.Core;

namespace SurfaceQ.Cli;

// `surfaceq ficd-init`: seed a starter `ficd/` authoring folder into every
// library under the workspace so `surfaceq ficd` has something to render. Each
// seed file is written only if it does not already exist — an author's edits and
// hand-written sections are never overwritten. The author fills the placeholder
// TODOs and uncomments the `groups`, then runs `surfaceq ficd`. Exits 2 only when
// no library is found or a file cannot be written.

internal static class FicdInitCommand
{
    private const string FicdDirName = "ficd";

    public static int Run(
        string? project,
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

        var scaffolder = new FicdScaffolder();
        var failed = false;
        foreach (var manifest in manifests)
        {
            if (!SeedLibrary(manifest, scaffolder, info, error))
            {
                failed = true;
            }
        }
        return failed ? 2 : 0;
    }

    private static bool SeedLibrary(
        string manifest, FicdScaffolder scaffolder, Action<string> info, Action<string> error)
    {
        var libDir = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        var ficdDir = Path.Combine(libDir, FicdDirName);
        var name = new DirectoryInfo(libDir).Name;
        var written = 0;
        var skipped = 0;
        foreach (var file in scaffolder.Scaffold(name))
        {
            var path = Path.GetFullPath(Path.Combine(ficdDir, file.RelativePath));
            if (File.Exists(path))
            {
                skipped++;
                continue;
            }
            if (!Write(path, file.Content, error))
            {
                return false;
            }
            written++;
        }
        info($"info: {name}: seeded {written} file(s), {skipped} already present in {ficdDir}");
        return true;
    }

    private static bool Write(string path, string content, Action<string> error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            error($"error: cannot write '{path}': {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            error($"error: cannot write '{path}': {ex.Message}");
            return false;
        }
    }
}
