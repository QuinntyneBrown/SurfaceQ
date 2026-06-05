// Acceptance Test
// Traces to: L2-035, L2-037, L2-038
// Description: `surfaceq ficd` generates a multi-file FICD document set for each
// library that has a `ficd/` folder — merging extracted API facts with authored
// metadata — honors --output, is deterministic, writes UTF-8/LF without BOM,
// skips libraries without a `ficd/` folder, and exits 2 on no eligible library or
// a source parse error.

using System.Text;
using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class FicdCommandTests
{
    private const string ServicesRel =
        "detailed-interface-definition/core-api/services.md";

    [Fact]
    public async Task Ficd_generates_document_set_for_a_library_with_ficd_metadata()
    {
        var ws = NewWorkspace();
        try
        {
            CreateAuthLibrary(ws);

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws }, console);

            Assert.Equal(0, exit);

            var outRoot = Path.Combine(ws, "libs", "auth", "docs", "ficd");
            var readme = File.ReadAllText(Path.Combine(outRoot, "README.md"), Encoding.UTF8);
            Assert.Contains("# Auth FICD", readme);
            Assert.Contains("## Document Set", readme);
            Assert.Contains($"| 1 | [Core API — Services]({ServicesRel}) | All plugin-facing services. |", readme);

            var services = File.ReadAllText(
                Path.Combine(outRoot, ServicesRel.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);
            Assert.Contains("# Core API — Services", services);
            Assert.Contains("| Document Title | Auth FICD |", services);
            Assert.Contains(
                "| Commanding | `ICommandService` | `COMMAND_SERVICE` | `CommandService` | Action |", services);
            Assert.Contains("### `ICommandService` / `COMMAND_SERVICE`", services);
            Assert.Contains("| `send` | Method | `send(command: string): boolean` | Sends a command. |", services);
            Assert.Contains("1. A plugin **shall** inject the token.", services);

            // The generated output never writes into the `ficd/` input folder.
            Assert.False(Directory.Exists(Path.Combine(ws, "libs", "auth", "ficd", "docs")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_honors_output_directory_relative_to_each_library()
    {
        var ws = NewWorkspace();
        try
        {
            CreateAuthLibrary(ws);

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws, "--output", "reports/ficd" }, new TestConsole());

            Assert.Equal(0, exit);
            Assert.True(File.Exists(Path.Combine(ws, "libs", "auth", "reports", "ficd", "README.md")));
            Assert.False(Directory.Exists(Path.Combine(ws, "libs", "auth", "docs", "ficd")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_is_deterministic_across_repeated_runs()
    {
        var ws = NewWorkspace();
        try
        {
            CreateAuthLibrary(ws);
            var path = Path.Combine(
                ws, "libs", "auth", "docs", "ficd",
                ServicesRel.Replace('/', Path.DirectorySeparatorChar));

            await Program.BuildRootCommand().InvokeAsync(new[] { "ficd", "--project", ws }, new TestConsole());
            var first = File.ReadAllText(path, Encoding.UTF8);
            await Program.BuildRootCommand().InvokeAsync(new[] { "ficd", "--project", ws }, new TestConsole());
            var second = File.ReadAllText(path, Encoding.UTF8);

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_output_is_utf8_no_bom_lf_only_and_preserves_non_ascii()
    {
        var ws = NewWorkspace();
        try
        {
            CreateAuthLibrary(ws, summary: "Maps input → output.");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws }, new TestConsole());
            Assert.Equal(0, exit);

            var bytes = File.ReadAllBytes(Path.Combine(
                ws, "libs", "auth", "docs", "ficd",
                ServicesRel.Replace('/', Path.DirectorySeparatorChar)));
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "output must not start with a UTF-8 BOM");
            Assert.DoesNotContain((byte)0x0D, bytes);
            Assert.Contains("Maps input → output.", Encoding.UTF8.GetString(bytes));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_refuses_to_write_into_the_ficd_input_folder()
    {
        var ws = NewWorkspace();
        try
        {
            CreateAuthLibrary(ws);
            var ymlPath = Path.Combine(ws, "libs", "auth", "ficd", "ficd.yml");
            var before = File.ReadAllText(ymlPath);

            var console = new TestConsole();
            // --output ficd resolves to the input folder; must be refused.
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws, "--output", "ficd" }, console);

            Assert.Equal(2, exit);
            Assert.Contains("must not be", console.Error.ToString());
            Assert.Equal(before, File.ReadAllText(ymlPath)); // input untouched
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_refuses_a_case_variant_of_the_ficd_input_folder()
    {
        var ws = NewWorkspace();
        try
        {
            CreateAuthLibrary(ws);

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws, "--output", "FICD" }, console);

            Assert.Equal(2, exit);
            Assert.Contains("must not be", console.Error.ToString());
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_rejects_an_absolute_output_path()
    {
        var ws = NewWorkspace();
        var absolute = Path.Combine(Path.GetTempPath(), "sq-abs-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateAuthLibrary(ws);

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws, "--output", absolute }, console);

            Assert.Equal(2, exit);
            Assert.Contains("relative", console.Error.ToString());
            Assert.False(Directory.Exists(absolute));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_is_idempotent_the_file_set_is_stable_across_runs()
    {
        var ws = NewWorkspace();
        try
        {
            CreateAuthLibrary(ws);
            var outRoot = Path.Combine(ws, "libs", "auth", "docs", "ficd");

            await Program.BuildRootCommand().InvokeAsync(new[] { "ficd", "--project", ws }, new TestConsole());
            var first = Snapshot(outRoot);
            await Program.BuildRootCommand().InvokeAsync(new[] { "ficd", "--project", ws }, new TestConsole());
            var second = Snapshot(outRoot);

            // Same file set (no phantom/duplicate files) and identical content.
            Assert.Equal(first.Keys.OrderBy(k => k, StringComparer.Ordinal),
                         second.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var key in first.Keys)
            {
                Assert.Equal(first[key], second[key]);
            }
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_ignores_an_authored_readme_and_generates_the_index()
    {
        var ws = NewWorkspace();
        try
        {
            CreateAuthLibrary(ws);
            // An authored README inside ficd/ must not become a section nor clobber
            // the generated Document Set index.
            File.WriteAllText(Path.Combine(ws, "libs", "auth", "ficd", "README.md"),
                "---\ntitle: Hand-written\n---\nDO NOT GENERATE OVER ME.\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws }, new TestConsole());
            Assert.Equal(0, exit);

            var readme = File.ReadAllText(
                Path.Combine(ws, "libs", "auth", "docs", "ficd", "README.md"), Encoding.UTF8);
            Assert.Contains("## Document Set", readme);
            Assert.DoesNotContain("DO NOT GENERATE OVER ME.", readme);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_exits_2_when_no_library_has_a_ficd_folder()
    {
        var ws = NewWorkspace();
        try
        {
            // A library with sources but no ficd/ folder is skipped.
            var libDir = Path.Combine(ws, "libs", "plain");
            var srcDir = Path.Combine(libDir, "src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(libDir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(srcDir, "public-api.ts"), "export * from './lib';\n");
            File.WriteAllText(Path.Combine(srcDir, "lib.ts"), "export interface X { a: string; }\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws }, console);

            Assert.Equal(2, exit);
            Assert.Contains("ficd/", console.Error.ToString());
            Assert.False(Directory.Exists(Path.Combine(libDir, "docs")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_exits_2_when_a_source_file_has_a_syntax_error()
    {
        var ws = NewWorkspace();
        try
        {
            var libDir = Path.Combine(ws, "libs", "broken");
            var srcDir = Path.Combine(libDir, "src");
            Directory.CreateDirectory(srcDir);
            Directory.CreateDirectory(Path.Combine(libDir, "ficd"));
            File.WriteAllText(Path.Combine(libDir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(libDir, "ficd", "ficd.yml"), "documentTitle: Broken FICD\n");
            File.WriteAllText(Path.Combine(srcDir, "public-api.ts"), "export * from './broken';\n");
            File.WriteAllText(Path.Combine(srcDir, "broken.ts"), "export class Broken {\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws }, console);

            Assert.Equal(2, exit);
            Assert.Contains("parse error", console.Error.ToString());
            Assert.False(Directory.Exists(Path.Combine(libDir, "docs")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    private static string NewWorkspace()
    {
        var ws = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ws);
        return ws;
    }

    // Maps each output file's POSIX relative path to its content, for set+content
    // comparison across runs.
    private static Dictionary<string, string> Snapshot(string root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            map[rel] = File.ReadAllText(file, Encoding.UTF8);
        }
        return map;
    }

    private static void CreateAuthLibrary(string ws, string summary = "The service surface.")
    {
        var libDir = Path.Combine(ws, "libs", "auth");
        var srcDir = Path.Combine(libDir, "src");
        var ficdDir = Path.Combine(libDir, "ficd", "detailed-interface-definition", "core-api");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(ficdDir);

        File.WriteAllText(Path.Combine(libDir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
        File.WriteAllText(Path.Combine(libDir, "package.json"), "{ \"name\": \"@acme/auth\" }");
        File.WriteAllText(Path.Combine(srcDir, "public-api.ts"), "export * from './lib';\n");
        File.WriteAllText(Path.Combine(srcDir, "lib.ts"),
            "import { InjectionToken } from '@angular/core';\n" +
            "export interface ICommandService {\n" +
            "  /** Sends a command. */\n" +
            "  send(command: string): boolean;\n" +
            "}\n" +
            "export const COMMAND_SERVICE = new InjectionToken<ICommandService>('COMMAND_SERVICE');\n" +
            "export class CommandService implements ICommandService {\n" +
            "  send(command: string): boolean { return true; }\n" +
            "}\n");

        File.WriteAllText(Path.Combine(libDir, "ficd", "ficd.yml"),
            "documentTitle: Auth FICD\n" +
            "version: 1.0 (Draft)\n" +
            "documents:\n" +
            "  - " + ServicesRel + "\n");

        File.WriteAllText(Path.Combine(ficdDir, "services.md"),
            "---\n" +
            "template: services\n" +
            "section: 8\n" +
            "title: Core API — Services\n" +
            "scope: All plugin-facing services.\n" +
            "summary: " + summary + "\n" +
            "inventory: true\n" +
            "groups:\n" +
            "  - title: Commanding\n" +
            "    heading: 8.1\n" +
            "    access: Action\n" +
            "    interfaces: [ICommandService]\n" +
            "    tokens: [COMMAND_SERVICE]\n" +
            "    classes: [CommandService]\n" +
            "requirements:\n" +
            "  - A plugin **shall** inject the token.\n" +
            "---\n" +
            "Idiomatic injection from a tile.\n");
    }
}
