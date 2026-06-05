using SurfaceQ.Core;

namespace SurfaceQ.Cli;

// `surfaceq providers`: generate an Angular `provide-<project>.ts` for every
// library under the workspace, wiring each `InjectionToken<IFoo>` to the exported
// class that implements `IFoo` (`useExisting`) and binding configuration tokens
// through a generated options object (`useValue`). The file is written at the
// common-ancestor directory of the wired declarations (overwriting in place). A
// library with no injectable contracts is skipped. All libraries are attempted;
// the command exits 2 if any fails, none are found, or a file cannot be written.

internal static class ProvidersCommand
{
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

        var failed = false;
        foreach (var manifest in manifests)
        {
            var (provider, exit) = ProviderPipeline.Build(manifest, info, trace, warn, error);
            if (exit != 0 || provider == null)
            {
                failed = true;
                continue;
            }
            foreach (var warning in provider.Warnings)
            {
                warn($"warn: {warning}");
            }
            if (provider.BindingCount == 0)
            {
                info($"info: no injectable contracts found in '{Path.GetDirectoryName(manifest)}'; skipped");
                continue;
            }
            if (!Write(provider, info, error))
            {
                failed = true;
            }
        }
        return failed ? 2 : 0;
    }

    private static bool Write(GeneratedProvider provider, Action<string> info, Action<string> error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(provider.TargetFile)!);
            File.WriteAllText(provider.TargetFile, provider.Content);
        }
        catch (UnauthorizedAccessException ex)
        {
            error($"error: cannot write '{provider.TargetFile}': {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            error($"error: cannot write '{provider.TargetFile}': {ex.Message}");
            return false;
        }
        info($"info: wrote {provider.TargetFile}");
        return true;
    }
}
