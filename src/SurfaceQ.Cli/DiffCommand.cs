namespace SurfaceQ.Cli;

internal static class DiffCommand
{
    public static int Run(string? project, Action<string> info, Action<string> warn, Action<string> error)
    {
        var result = OutputPipeline.Build(project, info, warn, error);
        if (result.ExitCode != 0)
        {
            return result.ExitCode;
        }
        var entryFile = result.Context!.EntryFile;
        string actual;
        try
        {
            actual = File.ReadAllText(entryFile);
        }
        catch (FileNotFoundException)
        {
            error($"error: public-api.ts not found at '{entryFile}'");
            return 2;
        }
        catch (UnauthorizedAccessException ex)
        {
            error($"error: cannot read '{entryFile}': {ex.Message}");
            return 2;
        }
        catch (IOException ex)
        {
            error($"error: cannot read '{entryFile}': {ex.Message}");
            return 2;
        }
        if (StringComparer.Ordinal.Equals(result.Output, actual))
        {
            return 0;
        }
        return 1;
    }
}
