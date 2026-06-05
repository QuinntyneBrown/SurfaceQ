using SurfaceQ.Core;

namespace SurfaceQ.Cli;

// Builds the FICD output files for one library: extract its public API (reusing
// the docs `document` extraction, implementations included so concrete classes
// can be named), read the authored `ficd/` metadata, and render the document set.
// Parallels DocumentationPipeline / InventoryPipeline; the host owns grouping,
// ordering and rendering.

internal static class FicdPipeline
{
    public static (IReadOnlyList<FicdOutputFile>? Files, int ExitCode) Build(
        string manifestPath,
        string ficdDir,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error)
    {
        var (api, exit) = DocumentationPipeline.Build(
            manifestPath, includeImplementations: true, info, trace, warn, error);
        if (exit != 0 || api == null)
        {
            return (null, 2);
        }
        var project = new FicdMetadataReader().Read(ficdDir, api.Name);
        trace($"trace: read {project.Sections.Count} ficd section(s) for '{api.Name}'");
        var files = new FicdRenderer().Render(project, api);
        return (files, 0);
    }
}
