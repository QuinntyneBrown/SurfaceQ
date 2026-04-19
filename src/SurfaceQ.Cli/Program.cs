using System.CommandLine;

namespace SurfaceQ.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        return BuildRootCommand().InvokeAsync(args);
    }

    internal static RootCommand BuildRootCommand()
    {
        return new RootCommand("SurfaceQ — Explicit Public API Generator for Angular Libraries");
    }
}
