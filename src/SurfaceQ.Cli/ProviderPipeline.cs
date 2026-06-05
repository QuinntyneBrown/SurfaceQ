using SurfaceQ.Core;

namespace SurfaceQ.Cli;

// Builds the GeneratedProvider for one library: extract its API (reusing the docs
// `document` extraction, implementations included so concrete classes are
// visible), then ask ProviderGenerator to wire interface+token+class triples and
// config tokens into a provide-<project>.ts. The project name is the library
// directory name (the Angular project name). Parallels DocumentationPipeline.

internal static class ProviderPipeline
{
    public static (GeneratedProvider? Provider, int ExitCode) Build(
        string manifestPath,
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
        var projectName = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!).Name;
        var provider = new ProviderGenerator().Generate(api, projectName);
        trace($"trace: {projectName}: {provider.BindingCount} binding(s) for {provider.FunctionName}");
        return (provider, 0);
    }
}
