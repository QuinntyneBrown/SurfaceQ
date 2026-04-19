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
        var root = new RootCommand("SurfaceQ — Explicit Public API Generator for Angular Libraries");
        root.AddCommand(new Command("generate", "Write public-api.ts to disk."));
        root.AddCommand(new Command("check", "Verify public-api.ts matches expected output."));
        root.AddCommand(new Command("diff", "Print a unified diff between expected and actual output."));
        return root;
    }
}
