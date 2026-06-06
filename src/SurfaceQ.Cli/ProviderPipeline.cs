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

    // Folder mode: scan an arbitrary directory (and its subfolders) as a single unit
    // and wire one provide-<folder>.ts anchored at that folder. Parallels Build but
    // skips ng-package.json discovery — the folder itself is the scan root.
    public static (GeneratedProvider? Provider, int ExitCode) BuildFromFolder(
        string folderPath,
        Action<string> info,
        Action<string> trace,
        Action<string> warn,
        Action<string> error)
    {
        var (api, exit) = DocumentationPipeline.BuildFromFolder(
            folderPath, includeImplementations: true, info, trace, warn, error);
        if (exit != 0 || api == null)
        {
            return (null, 2);
        }
        var folderFull = Path.GetFullPath(folderPath);
        var folderName = new DirectoryInfo(folderFull).Name;
        var provider = new ProviderGenerator().Generate(api, folderName, folderFull);
        trace($"trace: {folderName}: {provider.BindingCount} binding(s) for {provider.FunctionName}");
        return (provider, 0);
    }
}
