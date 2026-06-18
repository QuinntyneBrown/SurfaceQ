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

    private static Command BuildGenerateCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var onlyPublicApiOption = OnlyPublicApiOption();
        var command = new Command("generate", "Write public-api.ts to disk.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.AddOption(onlyPublicApiOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var onlyPublicApi = ctx.ParseResult.GetValueForOption(onlyPublicApiOption);
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = GenerateCommand.Run(
                project, onlyPublicApi, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Command BuildCheckCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var onlyPublicApiOption = OnlyPublicApiOption();
        var command = new Command("check", "Verify public-api.ts matches expected output.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.AddOption(onlyPublicApiOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var onlyPublicApi = ctx.ParseResult.GetValueForOption(onlyPublicApiOption);
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = CheckCommand.Run(
                project, onlyPublicApi, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Command BuildDiffCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var onlyPublicApiOption = OnlyPublicApiOption();
        var command = new Command("diff", "Print a unified diff between expected and actual output.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.AddOption(onlyPublicApiOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var onlyPublicApi = ctx.ParseResult.GetValueForOption(onlyPublicApiOption);
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = DiffCommand.Run(
                project, onlyPublicApi, writers.Info, writers.Trace, writers.Warn, writers.Error);
        });
        return command;
    }

    private static Option<bool> OnlyPublicApiOption() =>
        new(
            "--only-public-api",
            "Include only declarations whose JSDoc carries the @publicApi tag.");

    private static Command BuildDocsCommand()
    {
        var projectOption = ProjectOption();
        var verbosityOption = VerbosityOption();
        var outputOption = OutputOption();
        var includeImplementationsOption = IncludeImplementationsOption();
        var servicesOption = ServicesOption();
        var includeDeprecatedTypesOption = IncludeDeprecatedTypesOption();
        var command = new Command("docs", "Document each library's public API as Markdown.");
        command.AddOption(projectOption);
        command.AddOption(verbosityOption);
        command.AddOption(outputOption);
        command.AddOption(includeImplementationsOption);
        command.AddOption(servicesOption);
        command.AddOption(includeDeprecatedTypesOption);
        command.SetHandler((InvocationContext ctx) =>
        {
            var project = ctx.ParseResult.GetValueForOption(projectOption);
            var verbosity = ctx.ParseResult.GetValueForOption(verbosityOption) ?? "normal";
            var includeImplementations = ctx.ParseResult.GetValueForOption(includeImplementationsOption);
            var services = ctx.ParseResult.GetValueForOption(servicesOption);
            var includeDeprecatedTypes = ctx.ParseResult.GetValueForOption(includeDeprecatedTypesOption);
            // An explicit --output always wins; otherwise --services picks SERVICE_API.md.
            var output = ctx.ParseResult.GetValueForOption(outputOption)
                ?? (services ? "SERVICE_API.md" : "API.md");
            var writers = Writers.For(ctx.Console, verbosity);
            ctx.ExitCode = DocsCommand.Run(
                project, output, includeImplementations, services, includeDeprecatedTypes,
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

    private static Option<string?> OutputOption() =>
        // No default value here: when unset, the docs handler resolves it to
        // API.md, or SERVICE_API.md when --services is passed.
        new(
            "--output",
            "Markdown file path, relative to each library directory " +
            "(default API.md, or SERVICE_API.md with --services).");

    private static Option<bool> ServicesOption() =>
        new(
            "--services",
            "Document only Angular services (@Injectable classes) into SERVICE_API.md.");

    private static Option<bool> IncludeDeprecatedTypesOption() =>
        new(
            "--include-deprecated-types",
            "Include declarations tagged @deprecated (excluded by default).");

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
