using SurfaceQ.Core;

namespace SurfaceQ.Cli;

internal static class GenerateCommand
{
    public static int Run(string? project, Action<string> info, Action<string> error)
    {
        var startPath = ResolveStartPath(project);
        var manifest = new ProjectLocator().Locate(startPath);
        if (manifest == null)
        {
            error($"error: could not find ng-package.json searching from '{startPath}'");
            return 2;
        }
        ProjectContext context;
        try
        {
            context = new ManifestReader().Read(manifest, info);
        }
        catch (ManifestException ex)
        {
            error(ex.Message);
            return 2;
        }

        var sources = new SourceFileWalker().Walk(context);
        var files = sources
            .Select(path => new FileExports(path, Array.Empty<string>(), Array.Empty<string>()))
            .ToList();
        var output = new PublicApiRenderer().Render(files, context);
        File.WriteAllText(context.EntryFile, output);
        info($"info: wrote {context.EntryFile}");
        return 0;
    }

    private static string ResolveStartPath(string? project)
    {
        if (string.IsNullOrWhiteSpace(project))
        {
            return Directory.GetCurrentDirectory();
        }
        if (File.Exists(project))
        {
            return Path.GetDirectoryName(Path.GetFullPath(project))!;
        }
        return Path.GetFullPath(project);
    }
}
