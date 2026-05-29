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
        root.AddCommand(BuildCheckCommand());
        root.AddCommand(BuildDiffCommand());
        root.AddCommand(BuildDocsCommand());
        return root;
    }

    private static Command BuildGenerateCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var command = new Command("generate", "Write public-api.ts to disk.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = GenerateCommand.Run(project, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Command BuildCheckCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var command = new Command("check", "Verify public-api.ts matches expected output.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = CheckCommand.Run(project, writers.Info, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Command BuildDiffCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var command = new Command("diff", "Print a unified diff between expected and actual output.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = DiffCommand.Run(project, writers.Info, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Command BuildDocsCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var outputOption = OutputOption();
        var command = new Command("docs", "Document each library's public API as Markdown.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.AddOption(outputOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "API.md";
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = DocsCommand.Run(project, output, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Option<string?> OutputOption()
    {
        var option = new Option<string?>(
            "--output",
            "Markdown file path, relative to each library directory.");
        option.SetDefaultValue("API.md");
        return option;
    }

    private static Option<string?> ProjectOption() =>
        new("--project", "Path to the project directory or ng-package.json file.");

    private static Option<string?> VerbosityOption()
    {
        var option = new Option<string?>(
            "--verbosity",
            "Logging verbosity: quiet, minimal, normal, detailed, diagnostic.");
        option.SetDefaultValue("normal");
        return option;
    }
}
