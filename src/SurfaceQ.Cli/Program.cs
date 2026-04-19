using System.CommandLine;
using System.CommandLine.Invocation;
using SurfaceQ.Core;

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
        root.AddCommand(BuildGenerateCommand());
        root.AddCommand(new Command("check", "Verify public-api.ts matches expected output."));
        root.AddCommand(new Command("diff", "Print a unified diff between expected and actual output."));
        return root;
    }

    private static Command BuildGenerateCommand()
    {
        var projectOption = new Option<string?>("--project", "Path to the project directory or ng-package.json file.");
        var command = new Command("generate", "Write public-api.ts to disk.");
        command.AddOption(projectOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var console = ctx.Console;
            Action<string> toStdout = message => console.Out.Write(message + Environment.NewLine);
            Action<string> toStderr = message => console.Error.Write(message + Environment.NewLine);
            ctx.ExitCode = GenerateCommand.Run(project, toStdout, toStderr, toStderr);
        });
        return command;
    }
}
