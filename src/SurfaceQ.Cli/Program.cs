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
        root.AddCommand(BuildInventoryCommand());
        root.AddCommand(BuildFicdInitCommand());
        root.AddCommand(BuildFicdCommand());
        root.AddCommand(BuildProvidersCommand());
        return root;
    }

    private static Command BuildProvidersCommand()
    {
        var projectOption = ProjectOption();
        var folderOption = ProvidersFolderOption();
        var verbosityOption = VerbosityOption();
        var command = new Command(
            "providers",
            "Generate an Angular provide-<project>.ts (per library, or per --folder) " +
            "that wires interfaces, tokens, and implementations.");
        command.AddOption(projectOption);
        command.AddOption(folderOption);
        command.AddOption(verbosityOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var folder = ctx.ParseResult.GetValueForOption(folderOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = ProvidersCommand.Run(
                project, folder, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Option<string?> ProvidersFolderOption() =>
        new(
            "--folder",
            "Generate a single provide-<folder>.ts for this folder and its subfolders. " +
            "Takes precedence over --project (the workspace is not scanned when set).");

    private static Command BuildFicdInitCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var command = new Command(
            "ficd-init",
            "Seed a starter ficd/ metadata folder into each library for the ficd command.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = FicdInitCommand.Run(
                project, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
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
        var includeImplementationsOption = IncludeImplementationsOption();
        var command = new Command("docs", "Document each library's public API as Markdown.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.AddOption(outputOption);
        command.AddOption(includeImplementationsOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "API.md";
            var includeImplementations = ctx.ParseResult.GetValueForOption(includeImplementationsOption);
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = DocsCommand.Run(
                project, output, includeImplementations,
                writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Command BuildInventoryCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var outputOption = InventoryOutputOption();
        var command = new Command(
            "inventory",
            "Inventory every developer-authored object (apps + libraries) as Markdown.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.AddOption(outputOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "INVENTORY.md";
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = InventoryCommand.Run(
                project, output, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Command BuildFicdCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var outputOption = FicdOutputOption();
        var command = new Command(
            "ficd",
            "Generate a Functional Interface Control Document per library from its ficd/ metadata.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.AddOption(outputOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var output = ctx.ParseResult.GetValueForOption(outputOption) ?? "docs/ficd";
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = FicdCommand.Run(
                project, output, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Option<string?> FicdOutputOption()
    {
        var option = new Option<string?>(
            "--output",
            "Output directory for the FICD document set, relative to each library directory.");
        option.SetDefaultValue("docs/ficd");
        return option;
    }

    private static Option<string?> OutputOption()
    {
        var option = new Option<string?>(
            "--output",
            "Markdown file path, relative to each library directory.");
        option.SetDefaultValue("API.md");
        return option;
    }

    private static Option<string?> InventoryOutputOption()
    {
        var option = new Option<string?>(
            "--output",
            "Markdown file path, relative to each project directory.");
        option.SetDefaultValue("INVENTORY.md");
        return option;
    }

    private static Option<bool> IncludeImplementationsOption() =>
        new(
            "--include-implementations",
            "Include classes that implement an exported interface (hidden by default).");

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
