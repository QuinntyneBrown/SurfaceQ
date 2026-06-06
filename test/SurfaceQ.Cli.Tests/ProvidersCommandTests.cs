// Acceptance Test
// Traces to: L2-040
// Description: `surfaceq providers` generates a provide-<project>.ts for each
// library — binding interface+token+class triples (useExisting) and config tokens
// (useValue) — written at the common-ancestor directory, deterministically; skips
// libraries with no injectable contracts; and exits 2 on no library or a parse error.

using System.Text;
using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class ProvidersCommandTests
{
    [Fact]
    public async Task Generates_provide_file_with_useExisting_and_useValue()
    {
        var ws = NewWorkspace();
        try
        {
            CreateApiLibrary(ws);

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);
            var provide = Path.Combine(ws, "libs", "api", "src", "lib", "provide-api.ts");
            Assert.True(File.Exists(provide));
            var content = File.ReadAllText(provide, Encoding.UTF8);
            Assert.Contains("export function provideApi(options: ProvideApiOptions): EnvironmentProviders {", content);
            Assert.Contains("{ provide: API_BASE_URL, useValue: options.apiBaseUrl },", content);
            Assert.Contains("{ provide: BILLS_SERVICE, useExisting: BillsService },", content);
            Assert.Contains("import { BillsService } from './services/bills.service';", content);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Skips_a_library_with_no_injectable_contracts()
    {
        var ws = NewWorkspace();
        try
        {
            var lib = Path.Combine(ws, "libs", "models");
            var src = Path.Combine(lib, "src", "lib");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(lib, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(lib, "src", "public-api.ts"), "export * from './lib/foo';\n");
            File.WriteAllText(Path.Combine(src, "foo.ts"), "export interface Foo { id: string; }\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--project", ws }, console);

            Assert.Equal(0, exit);
            Assert.Contains("no injectable contracts", console.Out.ToString());
            Assert.Empty(Directory.GetFiles(lib, "provide-*.ts", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // Also guards that the DocumentationPipeline Build -> BuildFromContext extraction
    // (added for folder mode) keeps workspace mode byte-identical.
    [Fact]
    public async Task Is_deterministic_across_repeated_runs()
    {
        var ws = NewWorkspace();
        try
        {
            CreateApiLibrary(ws);
            var provide = Path.Combine(ws, "libs", "api", "src", "lib", "provide-api.ts");

            await Program.BuildRootCommand().InvokeAsync(new[] { "providers", "--project", ws }, new TestConsole());
            var first = File.ReadAllText(provide, Encoding.UTF8);
            await Program.BuildRootCommand().InvokeAsync(new[] { "providers", "--project", ws }, new TestConsole());
            var second = File.ReadAllText(provide, Encoding.UTF8);

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Exits_2_when_no_library_is_found()
    {
        var ws = NewWorkspace();
        try
        {
            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--project", Path.Combine(ws, "empty") }, console);

            Assert.Equal(2, exit);
            Assert.Contains("no ng-package.json", console.Error.ToString());
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Exits_2_when_a_source_file_has_a_syntax_error()
    {
        var ws = NewWorkspace();
        try
        {
            var lib = Path.Combine(ws, "libs", "broken");
            var src = Path.Combine(lib, "src");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(lib, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(src, "public-api.ts"), "export * from './broken';\n");
            File.WriteAllText(Path.Combine(src, "broken.ts"), "export class Broken {\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--project", ws }, console);

            Assert.Equal(2, exit);
            Assert.Contains("parse error", console.Error.ToString());
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // Acceptance Test
    // Traces to: L2-041
    // Description: `surfaceq providers --folder <dir>` treats the directory as a
    // self-contained scan root (no ng-package.json), wiring tokens found in it and
    // its subfolders into a single provide-<folder>.ts written at the folder root.
    [Fact]
    public async Task Folder_mode_generates_provide_file_for_the_folder_and_subfolders()
    {
        var ws = NewWorkspace();
        try
        {
            var folder = Path.Combine(ws, "infrastructure");
            CreateFolderInjectables(folder);

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--folder", folder }, console);

            Assert.Equal(0, exit);
            var provide = Path.Combine(folder, "provide-infrastructure.ts");
            Assert.True(File.Exists(provide), console.Out.ToString() + console.Error.ToString());
            var content = File.ReadAllText(provide, Encoding.UTF8);
            Assert.Contains(
                "export function provideInfrastructure(options: ProvideInfrastructureOptions): EnvironmentProviders {",
                content);
            // Config token nested two levels below the folder is discovered (recursive).
            Assert.Contains("{ provide: API_BASE_URL, useValue: options.apiBaseUrl },", content);
            Assert.Contains("readonly apiBaseUrl: string;", content);
            // Service binding in a subfolder, imported by a relative path from the folder root.
            Assert.Contains("{ provide: BILLS_SERVICE, useExisting: BillsService },", content);
            Assert.Contains("import { BillsService } from './services/bills.service';", content);
            // A token in a file at the folder root itself is also discovered and wired.
            Assert.Contains("{ provide: APP_NAME, useValue: options.appName },", content);
            Assert.Contains("import { APP_NAME } from './app-name.token';", content);
            // Folder-mode output advertises the --folder command that reproduces it.
            Assert.Contains("// Regenerate with: surfaceq providers --folder infrastructure", content);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Folder_mode_is_deterministic_across_repeated_runs()
    {
        var ws = NewWorkspace();
        try
        {
            var folder = Path.Combine(ws, "infrastructure");
            CreateFolderInjectables(folder);
            var provide = Path.Combine(folder, "provide-infrastructure.ts");

            await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--folder", folder }, new TestConsole());
            var first = File.ReadAllText(provide, Encoding.UTF8);
            await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--folder", folder }, new TestConsole());
            var second = File.ReadAllText(provide, Encoding.UTF8);

            Assert.Equal(first, second);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Folder_mode_exits_2_when_the_folder_does_not_exist()
    {
        var ws = NewWorkspace();
        try
        {
            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--folder", Path.Combine(ws, "nope") }, console);

            Assert.Equal(2, exit);
            Assert.Contains("does not exist", console.Error.ToString());
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Folder_mode_skips_a_folder_with_no_injectable_contracts()
    {
        var ws = NewWorkspace();
        try
        {
            var folder = Path.Combine(ws, "models");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "foo.ts"), "export interface Foo { id: string; }\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "providers", "--folder", folder }, console);

            Assert.Equal(0, exit);
            Assert.Contains("no injectable contracts", console.Out.ToString());
            Assert.Empty(Directory.GetFiles(folder, "provide-*.ts", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // A bare folder (no ng-package.json) with a token at the folder root, a config
    // token nested two levels deep, and an interface+token+class service binding
    // under a subfolder — exercising recursive discovery at every depth.
    private static void CreateFolderInjectables(string folder)
    {
        var services = Path.Combine(folder, "services");
        var config = Path.Combine(folder, "config", "tokens");
        Directory.CreateDirectory(services);
        Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(folder, "app-name.token.ts"),
            "import { InjectionToken } from '@angular/core';\n" +
            "export const APP_NAME = new InjectionToken<string>('APP_NAME');\n");
        File.WriteAllText(Path.Combine(config, "api-base-url.token.ts"),
            "import { InjectionToken } from '@angular/core';\n" +
            "export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');\n");
        File.WriteAllText(Path.Combine(services, "bills.service.contract.ts"),
            "import { InjectionToken } from '@angular/core';\n" +
            "export interface IBillsService { list(): boolean; }\n" +
            "export const BILLS_SERVICE = new InjectionToken<IBillsService>('BILLS_SERVICE');\n");
        File.WriteAllText(Path.Combine(services, "bills.service.ts"),
            "import { Injectable } from '@angular/core';\n" +
            "import { IBillsService } from './bills.service.contract';\n" +
            "@Injectable({ providedIn: 'root' })\n" +
            "export class BillsService implements IBillsService { list(): boolean { return true; } }\n");
    }

    private static string NewWorkspace()
    {
        var ws = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ws);
        return ws;
    }

    private static void CreateApiLibrary(string ws, string name = "api")
    {
        var lib = Path.Combine(ws, "libs", name);
        var svc = Path.Combine(lib, "src", "lib", "services");
        Directory.CreateDirectory(svc);
        File.WriteAllText(Path.Combine(lib, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
        File.WriteAllText(Path.Combine(lib, "src", "public-api.ts"),
            "export * from './lib/services/bills.service.contract';\n");
        File.WriteAllText(Path.Combine(lib, "src", "lib", "api-base-url.token.ts"),
            "import { InjectionToken } from '@angular/core';\n" +
            "export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');\n");
        File.WriteAllText(Path.Combine(svc, "bills.service.contract.ts"),
            "import { InjectionToken } from '@angular/core';\n" +
            "export interface IBillsService { list(): boolean; }\n" +
            "export const BILLS_SERVICE = new InjectionToken<IBillsService>('BILLS_SERVICE');\n");
        File.WriteAllText(Path.Combine(svc, "bills.service.ts"),
            "import { Injectable } from '@angular/core';\n" +
            "import { IBillsService } from './bills.service.contract';\n" +
            "@Injectable({ providedIn: 'root' })\n" +
            "export class BillsService implements IBillsService { list(): boolean { return true; } }\n");
    }
}
