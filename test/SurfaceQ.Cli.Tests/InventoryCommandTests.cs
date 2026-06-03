// Acceptance Test
// Traces to: L2-031
// Description: `surfaceq inventory` discovers each application and library in a
// workspace and writes one INVENTORY.md per project (apps via angular.json, libs
// via ng-package.json), honors --output, is deterministic, and exits 2 when the
// target contains no projects.

using System.Text;
using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class InventoryCommandTests
{
    [Fact]
    public async Task Inventory_writes_one_document_per_app_and_library()
    {
        var ws = NewWorkspace();
        try
        {
            CreateApp(ws);
            CreateLibrary(ws);

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "inventory", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);

            var appDoc = File.ReadAllText(Path.Combine(ws, "apps", "web", "INVENTORY.md"), Encoding.UTF8);
            Assert.Contains("# web — Inventory", appDoc);
            Assert.Contains("_Type: application_", appDoc);
            Assert.Contains("## Components", appDoc);
            Assert.Contains("| `AppComponent` | `class` | yes | `src/app/app.component.ts` |", appDoc);
            // An internal (non-exported) class is inventoried and marked.
            Assert.Contains("| `InternalState` | `class` | no | `src/app/app.component.ts` | – |", appDoc);
            // A *.spec.ts file is excluded from the walk: its class never appears.
            Assert.DoesNotContain("ShouldBeIgnored", appDoc);

            var libDoc = File.ReadAllText(Path.Combine(ws, "libs", "auth", "INVENTORY.md"), Encoding.UTF8);
            Assert.Contains("# @acme/auth — Inventory", libDoc);
            Assert.Contains("_Type: library_", libDoc);
            Assert.Contains("## Interfaces", libDoc);
            Assert.Contains("| `AuthService` | `interface` | yes | `src/lib.ts` |", libDoc);
            Assert.Contains("## Injection Tokens", libDoc);
            Assert.Contains("| `AUTH` | `const` | yes | `src/lib.ts` |", libDoc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Inventory_honors_output_path_relative_to_each_project()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws);

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "inventory", "--project", ws, "--output", "reports/INVENTORY.md" }, new TestConsole());

            Assert.Equal(0, exit);
            Assert.True(File.Exists(Path.Combine(ws, "libs", "auth", "reports", "INVENTORY.md")));
            Assert.False(File.Exists(Path.Combine(ws, "libs", "auth", "INVENTORY.md")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Inventory_is_deterministic_across_repeated_runs()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws);
            var path = Path.Combine(ws, "libs", "auth", "INVENTORY.md");

            await Program.BuildRootCommand().InvokeAsync(new[] { "inventory", "--project", ws }, new TestConsole());
            var first = File.ReadAllText(path, Encoding.UTF8);
            await Program.BuildRootCommand().InvokeAsync(new[] { "inventory", "--project", ws }, new TestConsole());
            var second = File.ReadAllText(path, Encoding.UTF8);

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Inventory_exits_2_when_a_source_file_has_a_syntax_error()
    {
        var ws = NewWorkspace();
        try
        {
            var libDir = Path.Combine(ws, "libs", "broken");
            var srcDir = Path.Combine(libDir, "src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(libDir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            // Unterminated class body -> the sidecar reports a parse error.
            File.WriteAllText(Path.Combine(srcDir, "broken.ts"), "export class Broken {\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "inventory", "--project", ws }, console);

            Assert.Equal(2, exit);
            Assert.Contains("parse error", console.Error.ToString());
            Assert.False(File.Exists(Path.Combine(libDir, "INVENTORY.md")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // Defends L2-034 (LF endings) and L2-012 (byte-identical across hosts): the
    // output file is UTF-8 without a BOM, LF-only, and a non-ASCII source comment
    // survives as its real bytes rather than the OEM-code-page mojibake.
    [Fact]
    public async Task Inventory_output_is_utf8_no_bom_lf_only_and_preserves_non_ascii()
    {
        var ws = NewWorkspace();
        try
        {
            var libDir = Path.Combine(ws, "libs", "widgets");
            var srcDir = Path.Combine(libDir, "src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(libDir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(libDir, "package.json"), "{ \"name\": \"@acme/widgets\" }");
            File.WriteAllText(Path.Combine(srcDir, "public-api.ts"), "export * from './widget';\n");
            // → is RIGHTWARDS ARROW (UTF-8 E2 86 92); the same literal is used in
            // the fixture and the assertion, so the round-trip check is exact.
            File.WriteAllText(Path.Combine(srcDir, "widget.ts"),
                "/** Maps input → output. */\nexport class Widget {}\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "inventory", "--project", ws }, new TestConsole());
            Assert.Equal(0, exit);

            var bytes = File.ReadAllBytes(Path.Combine(libDir, "INVENTORY.md"));
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "output must not start with a UTF-8 BOM");
            Assert.DoesNotContain((byte)0x0D, bytes); // LF only, no CR
            Assert.Contains("Maps input → output.", Encoding.UTF8.GetString(bytes));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Inventory_exits_2_when_no_projects_found()
    {
        var ws = NewWorkspace();
        try
        {
            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "inventory", "--project", Path.Combine(ws, "empty") }, console);

            Assert.Equal(2, exit);
            Assert.Contains("no Angular", console.Error.ToString());
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

    private static void CreateApp(string ws)
    {
        var appSrc = Path.Combine(ws, "apps", "web", "src", "app");
        Directory.CreateDirectory(appSrc);
        File.WriteAllText(Path.Combine(ws, "angular.json"),
            "{ \"projects\": { \"web\": { \"projectType\": \"application\", " +
            "\"root\": \"apps/web\", \"sourceRoot\": \"apps/web/src\" } } }");
        File.WriteAllText(Path.Combine(appSrc, "app.component.ts"),
            "import { Component } from '@angular/core';\n" +
            "@Component({ selector: 'app-root', template: '' })\n" +
            "export class AppComponent {}\n" +
            "class InternalState {}\n");
        // A spec file must be excluded from the inventory.
        File.WriteAllText(Path.Combine(appSrc, "app.component.spec.ts"),
            "export class ShouldBeIgnored {}\n");
    }

    private static void CreateLibrary(string ws)
    {
        var libDir = Path.Combine(ws, "libs", "auth");
        var srcDir = Path.Combine(libDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(libDir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
        File.WriteAllText(Path.Combine(libDir, "package.json"), "{ \"name\": \"@acme/auth\" }");
        File.WriteAllText(Path.Combine(srcDir, "public-api.ts"), "export * from './lib';\n");
        File.WriteAllText(Path.Combine(srcDir, "lib.ts"),
            "import { InjectionToken } from '@angular/core';\n" +
            "export interface AuthService { login(user: string): boolean; }\n" +
            "export const AUTH = new InjectionToken<AuthService>('AUTH');\n");
    }
}
