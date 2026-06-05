using SurfaceQ.Core;

namespace SurfaceQ.Cli;

// `surfaceq ficd`: generate a Functional Interface Control Document for each
// library that opts in with a `ficd/` authoring folder next to its
// ng-package.json. The document set is written under the --output directory
// (default "docs/ficd", relative to each library). A library without a `ficd/`
// folder is skipped; the command exits 2 if none opt in, a source fails to
// parse, a document cannot be written, or the output directory would clobber the
// `ficd/` input. All eligible libraries are attempted.

internal static class FicdCommand
{
    private const string FicdDirName = "ficd";

    public static int Run(
        string? project,
        string output,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error)
    {
        var root = string.IsNullOrWhiteSpace(project)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(project);

        // --output is documented as relative to each library; an absolute path
        // would make every library write into one shared directory and clobber.
        if (Path.IsPathRooted(output))
        {
            error($"error: --output must be a relative path (got '{output}')");
            return 2;
        }

        var manifests = new WorkspaceLocator().FindLibraries(root);
        if (manifests.Count == 0)
        {
            error($"error: no ng-package.json libraries found under '{root}'");
            return 2;
        }

        var eligible = manifests.Where(HasFicdFolder).ToList();
        if (eligible.Count == 0)
        {
            error($"error: no library with a '{FicdDirName}/' metadata folder found under '{root}'");
            return 2;
        }
        trace($"trace: {eligible.Count} library(ies) have a {FicdDirName}/ folder");

        var failed = false;
        foreach (var manifest in eligible)
        {
            var libDir = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
            var ficdDir = Path.Combine(libDir, FicdDirName);
            var (files, exit) = FicdPipeline.Build(manifest, ficdDir, info, trace, warn, error);
            if (exit != 0 || files == null)
            {
                failed = true;
                continue;
            }
            if (!WriteAll(libDir, ficdDir, output, files, info, error))
            {
                failed = true;
            }
        }
        return failed ? 2 : 0;
    }

    private static bool HasFicdFolder(string manifest)
    {
        var libDir = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        return Directory.Exists(Path.Combine(libDir, FicdDirName));
    }

    private static bool WriteAll(
        string libDir,
        string ficdDir,
        string output,
        IReadOnlyList<FicdOutputFile> files,
        Action<string> info,
        Action<string> error)
    {
        var outputRoot = Path.GetFullPath(Path.Combine(libDir, output));
        if (IsUnderOrEqual(outputRoot, Path.GetFullPath(ficdDir)))
        {
            error($"error: --output directory must not be the '{FicdDirName}/' metadata folder " +
                  $"or a subdirectory of it ('{outputRoot}')");
            return false;
        }
        foreach (var file in files)
        {
            if (!WriteOne(outputRoot, file, error))
            {
                return false;
            }
        }
        info($"info: wrote {files.Count} file(s) to {outputRoot}");
        return true;
    }

    private static bool WriteOne(string outputRoot, FicdOutputFile file, Action<string> error)
    {
        var path = Path.GetFullPath(Path.Combine(outputRoot, file.RelativePath));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Content);
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
        return true;
    }

    // Conservative cross-platform guard: is `outputRoot` the `ficdDir` itself or a
    // descendant of it? Compared case-insensitively so the generator never writes
    // into (and then re-reads) the `ficd/` input on a case-insensitive filesystem
    // (Windows, default macOS). Catches both `--output ficd` and `--output ficd/out`.
    private static bool IsUnderOrEqual(string outputRoot, string ficdDir)
    {
        var output = TrimTrailingSeparators(outputRoot);
        var ficd = TrimTrailingSeparators(ficdDir);
        if (string.Equals(output, ficd, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return output.StartsWith(ficd + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimTrailingSeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
