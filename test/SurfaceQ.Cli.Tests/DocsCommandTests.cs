// Acceptance Test
// Traces to: L2-023, L2-027, L2-028
// Description: `surfaceq docs` writes one Markdown file per library next to its
// ng-package.json, names the library from package.json, honors --output, and
// exits 2 when the workspace contains no libraries.

using System.CommandLine;
using System.CommandLine.IO;
using System.Text;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class DocsCommandTests
{
    [Fact]
    public async Task Docs_writes_api_markdown_per_library()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "import { InjectionToken } from '@angular/core';\n" +
                "export interface AuthService { login(user: string): boolean; }\n" +
                "export const AUTH = new InjectionToken<AuthService>('AUTH');\n");
            CreateLibrary(ws, "data", null,
                "export enum Status { Active, Archived }\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws }, console);

            Assert.Equal(0, exit);

            var authDoc = File.ReadAllText(Path.Combine(ws, "libs", "auth", "API.md"), Encoding.UTF8);
            Assert.Contains("# @acme/auth — Public API", authDoc);
            Assert.Contains("## Interfaces", authDoc);
            Assert.Contains("| `login` | `user: string` | `boolean` | no | – |", authDoc);
            Assert.Contains("## Injection Tokens", authDoc);
            Assert.Contains("| `AUTH` | `AuthService` |", authDoc);

            var dataDoc = File.ReadAllText(Path.Combine(ws, "libs", "data", "API.md"), Encoding.UTF8);
            // No package.json name -> falls back to the directory name.
            Assert.Contains("# data — Public API", dataDoc);
            Assert.Contains("| `Active` | `0` | no | – |", dataDoc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Docs_honors_output_path_relative_to_each_library()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth", "export type Id = string;\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws, "--output", "docs/PUBLIC_API.md" }, console);

            Assert.Equal(0, exit);
            Assert.True(File.Exists(Path.Combine(ws, "libs", "auth", "docs", "PUBLIC_API.md")));
            Assert.False(File.Exists(Path.Combine(ws, "libs", "auth", "API.md")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Docs_hides_classes_implementing_an_exported_interface_by_default()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "import { InjectionToken } from '@angular/core';\n" +
                "export interface IThing { run(): void; }\n" +
                "export const THING = new InjectionToken<IThing>('THING');\n" +
                "export class Thing implements IThing { run(): void {} }\n" +
                "export class Widget { go(): void {} }\n" +
                "export class Adapter implements ExternalContract { go(): void {} }\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);
            var doc = File.ReadAllText(Path.Combine(ws, "libs", "auth", "API.md"), Encoding.UTF8);
            // The contract and token stay; the implementation behind the token is hidden.
            Assert.Contains("### `IThing`", doc);
            Assert.Contains("| `THING` | `IThing` |", doc);
            Assert.DoesNotContain("### `Thing`", doc);
            // Classes that do not implement an exported interface remain visible.
            Assert.Contains("### `Widget`", doc);
            Assert.Contains("### `Adapter`", doc); // implements a non-exported (external) interface
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Docs_includes_implementation_classes_when_flag_passed()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "export interface IThing { run(): void; }\n" +
                "export class Thing implements IThing { run(): void {} }\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws, "--include-implementations" }, new TestConsole());

            Assert.Equal(0, exit);
            var doc = File.ReadAllText(Path.Combine(ws, "libs", "auth", "API.md"), Encoding.UTF8);
            Assert.Contains("### `IThing`", doc);
            Assert.Contains("### `Thing`", doc);
            Assert.Contains("_Implements: `IThing`_", doc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Docs_marks_deprecated_declarations_and_members()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "/** @deprecated use Id */\n" +
                "export type OldId = string;\n" +
                "export interface Token {\n" +
                "  /** @deprecated use {@link Token.value} */\n" +
                "  raw: string;\n" +
                "  value: string;\n" +
                "}\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);
            var doc = File.ReadAllText(Path.Combine(ws, "libs", "auth", "API.md"), Encoding.UTF8);
            Assert.Contains("## Deprecations", doc);
            Assert.Contains("| `OldId` | type | use Id |", doc);
            Assert.Contains("| `Token.raw` | property | use {@link Token.value} |", doc);
            // Deprecated column populated in the type-alias table and member table.
            Assert.Contains("| `OldId` | `string` | use Id | – |", doc);
            Assert.Contains("| `raw` | `string` | no | use {@link Token.value} | – |", doc);
            Assert.Contains("| `value` | `string` | no | no | – |", doc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Docs_exits_2_when_no_libraries_found()
    {
        var ws = NewWorkspace();
        Directory.CreateDirectory(ws);
        try
        {
            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws }, console);

            Assert.Equal(2, exit);
            Assert.Contains("no ng-package.json libraries found", console.Error.ToString());
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Docs_is_deterministic_across_repeated_runs()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "/** @deprecated use B */\n" +
                "export interface A { /** @deprecated */ b(): void; c: number; }\n" +
                "/** @deprecated since v2 */\n" +
                "export type Old = string;\n" +
                "export enum E { X, Y }\n");
            var path = Path.Combine(ws, "libs", "auth", "API.md");

            await Program.BuildRootCommand().InvokeAsync(new[] { "docs", "--project", ws }, new TestConsole());
            var first = File.ReadAllText(path, Encoding.UTF8);
            await Program.BuildRootCommand().InvokeAsync(new[] { "docs", "--project", ws }, new TestConsole());
            var second = File.ReadAllText(path, Encoding.UTF8);

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    private static string NewWorkspace() =>
        Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));

    private static void CreateLibrary(string ws, string folder, string? packageName, string source)
    {
        var libDir = Path.Combine(ws, "libs", folder);
        var srcDir = Path.Combine(libDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(libDir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
        if (packageName != null)
        {
            File.WriteAllText(Path.Combine(libDir, "package.json"), "{ \"name\": \"" + packageName + "\" }");
        }
        File.WriteAllText(Path.Combine(srcDir, "public-api.ts"), "export * from './lib';\n");
        File.WriteAllText(Path.Combine(srcDir, "lib.ts"), source);
    }
}
