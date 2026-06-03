using SurfaceQ.Core;

namespace SurfaceQ.Cli;

// `surfaceq inventory`: a complete census of developer-authored objects in an
// Angular application, library, or workspace. One INVENTORY.md is written into
// each discovered project directory. The --output value is a path relative to
// each project directory (default "INVENTORY.md"). All projects are attempted;
// the command exits 2 if any project failed or none were found.

internal static class InventoryCommand
{
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

        var projects = new WorkspaceProjectLocator().Find(root);
        if (projects.Count == 0)
        {
            error($"error: no Angular applications or libraries found under '{root}'");
            return 2;
        }
        trace($"trace: found {projects.Count} project(s)");

        var renderer = new InventoryRenderer();
        var failed = false;
        foreach (var proj in projects)
        {
            var (inventory, exit) = InventoryPipeline.Build(proj, info, trace, warn, error);
            if (exit != 0 || inventory == null)
            {
                failed = true;
                continue;
            }
            if (!WriteDoc(proj.ProjectDir, output, renderer.Render(inventory), info, error))
            {
                failed = true;
            }
        }
        return failed ? 2 : 0;
    }

    private static bool WriteDoc(
        string projectDir,
        string output,
        string markdown,
        Action<string> info,
        Action<string> error)
    {
        var outputPath = Path.GetFullPath(Path.Combine(projectDir, output));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
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
