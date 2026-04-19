namespace SurfaceQ.Cli;

internal static class GenerateCommand
{
    public static int Run(string? project, Action<string> info, Action<string> warn, Action<string> error) =>
        Run(project, info, _ => { }, warn, error);

    public static int Run(string? project, Action<string> info, Action<string> trace, Action<string> warn, Action<string> error)
    {
        var result = OutputPipeline.Build(project, info, trace, warn, error);
        if (result.ExitCode != 0)
        {
            return result.ExitCode;
        }
        var entryFile = result.Context!.EntryFile;
        try
        {
            File.WriteAllText(entryFile, result.Output!);
        }
        catch (UnauthorizedAccessException ex)
        {
            error($"error: cannot write '{entryFile}': {ex.Message}");
            return 2;
        }
        catch (IOException ex)
        {
            error($"error: cannot write '{entryFile}': {ex.Message}");
            return 2;
        }
        info($"info: wrote {entryFile}");
        return 0;
    }
}
