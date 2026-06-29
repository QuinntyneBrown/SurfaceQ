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

    // Acceptance Test
    // Traces to: L2-030, L2-043
    // Description: With --include-deprecated-types, a deprecated declaration is kept
    // and rendered with its Deprecated column, callout, and Deprecations summary.
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

            // Deprecated declarations are excluded by default, so the flag is needed
            // to render OldId; the member-level deprecation on Token.raw would survive
            // either way (Token itself is not deprecated).
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(
                    new[] { "docs", "--project", ws, "--include-deprecated-types" }, new TestConsole());

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

    // Acceptance Test
    // Traces to: L2-042
    // Description: --services documents the service *contract* surface — the interfaces
    // an @Injectable implements, related models/types/enums, and their injection tokens
    // — and excludes the concrete service classes; it writes SERVICE_API.md (not API.md).
    [Fact]
    public async Task Docs_services_flag_documents_contracts_not_classes()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "import { Injectable, Component, InjectionToken } from '@angular/core';\n" +
                "export interface IAuthService { login(u: string): boolean; }\n" +
                "export interface Credentials { user: string; password: string; }\n" +
                "export type UserId = string;\n" +
                "export enum AuthRole { Admin, User }\n" +
                "export const AUTH_SERVICE = new InjectionToken<IAuthService>('AUTH_SERVICE');\n" +
                "@Injectable({ providedIn: 'root' })\n" +
                "export class AuthService implements IAuthService { login(u: string): boolean { return true; } }\n" +
                "export class PlainHelper { help(): void {} }\n" +
                "@Component({ selector: 'x-thing' })\n" +
                "export class ThingComponent {}\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws, "--services" }, new TestConsole());

            Assert.Equal(0, exit);
            var libDir = Path.Combine(ws, "libs", "auth");
            Assert.True(File.Exists(Path.Combine(libDir, "SERVICE_API.md")));
            Assert.False(File.Exists(Path.Combine(libDir, "API.md")));

            var doc = File.ReadAllText(Path.Combine(libDir, "SERVICE_API.md"), Encoding.UTF8);
            // The interface the service implements is documented, plus the supporting
            // models, types, and enums, and the injection token wiring the contract.
            Assert.Contains("## Interfaces", doc);
            Assert.Contains("### `IAuthService`", doc);
            Assert.Contains("### `Credentials`", doc);
            Assert.Contains("## Type Aliases", doc);
            Assert.Contains("| `UserId` | `string` |", doc);
            Assert.Contains("## Enums", doc);
            Assert.Contains("### `AuthRole`", doc);
            Assert.Contains("## Injection Tokens", doc);
            Assert.Contains("| `AUTH_SERVICE` | `IAuthService` |", doc);
            // A multi-section document keeps its table of contents.
            Assert.Contains("## Contents", doc);
            // The concrete service class and every other class are excluded.
            Assert.DoesNotContain("### `AuthService`", doc);
            Assert.DoesNotContain("## Classes", doc);
            Assert.DoesNotContain("### `PlainHelper`", doc);
            Assert.DoesNotContain("### `ThingComponent`", doc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // Acceptance Test
    // Traces to: L2-042
    // Description: An explicit --output overrides the SERVICE_API.md default that
    // --services would otherwise pick.
    [Fact]
    public async Task Docs_services_explicit_output_overrides_service_api_default()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "import { Injectable, InjectionToken } from '@angular/core';\n" +
                "export interface IAuthService { login(): boolean; }\n" +
                "export const AUTH_SERVICE = new InjectionToken<IAuthService>('AUTH_SERVICE');\n" +
                "@Injectable()\n" +
                "export class AuthService implements IAuthService { login(): boolean { return true; } }\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(
                    new[] { "docs", "--project", ws, "--services", "--output", "docs/SERVICES.md" },
                    new TestConsole());

            Assert.Equal(0, exit);
            var libDir = Path.Combine(ws, "libs", "auth");
            Assert.True(File.Exists(Path.Combine(libDir, "docs", "SERVICES.md")));
            Assert.False(File.Exists(Path.Combine(libDir, "SERVICE_API.md")));
            var doc = File.ReadAllText(Path.Combine(libDir, "docs", "SERVICES.md"), Encoding.UTF8);
            // The contract interface is documented; the concrete service class is not.
            Assert.Contains("### `IAuthService`", doc);
            Assert.DoesNotContain("### `AuthService`", doc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // Acceptance Test
    // Traces to: L2-043
    // Description: A declaration tagged @deprecated is omitted by default; a live
    // sibling stays, and the now-empty Deprecations summary is not rendered.
    [Fact]
    public async Task Docs_excludes_deprecated_types_by_default()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "/** @deprecated use NewThing */\n" +
                "export interface OldThing { a: string; }\n" +
                "export interface NewThing { b: string; }\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);
            var doc = File.ReadAllText(Path.Combine(ws, "libs", "auth", "API.md"), Encoding.UTF8);
            Assert.Contains("### `NewThing`", doc);
            Assert.DoesNotContain("### `OldThing`", doc);
            Assert.DoesNotContain("## Deprecations", doc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // Acceptance Test
    // Traces to: L2-043
    // Description: The default exclusion is declaration-level only — a deprecated
    // member inside a non-deprecated declaration is kept and still flagged.
    [Fact]
    public async Task Docs_keeps_deprecated_members_of_live_declarations_by_default()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "auth", "@acme/auth",
                "export interface Token {\n" +
                "  /** @deprecated use value */\n" +
                "  raw: string;\n" +
                "  value: string;\n" +
                "}\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);
            var doc = File.ReadAllText(Path.Combine(ws, "libs", "auth", "API.md"), Encoding.UTF8);
            Assert.Contains("### `Token`", doc);
            Assert.Contains("| `raw` | `string` | no | use value | – |", doc);
            // A surviving member-level deprecation still drives the summary.
            Assert.Contains("## Deprecations", doc);
            Assert.Contains("| `Token.raw` | property | use value |", doc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // Acceptance Test
    // Traces to: L2-044
    // Description: A function exposed as a callable interface + injection token is
    // documented with its call signature (parameters + return type), not "no members".
    [Fact]
    public async Task Docs_documents_callable_interface_behind_token()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "fmt", "@acme/fmt",
                "import { InjectionToken } from '@angular/core';\n" +
                "/** Formats a date. */\n" +
                "export interface FormatDate {\n" +
                "  (date: Date, format: string): string;\n" +
                "}\n" +
                "export const FORMAT_DATE = new InjectionToken<FormatDate>('FORMAT_DATE');\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);
            var doc = File.ReadAllText(Path.Combine(ws, "libs", "fmt", "API.md"), Encoding.UTF8);
            Assert.Contains("### `FormatDate`", doc);
            Assert.Contains("**Call signatures**", doc);
            Assert.Contains("| `date: Date, format: string` | `string` | no | – |", doc);
            Assert.DoesNotContain("_No public members._", doc);
            // The token still surfaces the contract type.
            Assert.Contains("| `FORMAT_DATE` | `FormatDate` |", doc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    // Acceptance Test
    // Traces to: L2-046
    // Description: `surfaceq docs --folder <dir>` treats the directory as a
    // self-contained scan root (no ng-package.json) and writes one API.md at the
    // folder root documenting just the declarations found in it and its subfolders.
    [Fact]
    public async Task Folder_mode_documents_a_single_folder_scope()
    {
        var ws = NewWorkspace();
        try
        {
            // A folder holding exactly one service contract: an interface, its token,
            // and the implementing class (hidden behind the token by default).
            var folder = Path.Combine(ws, "services", "auth");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "auth.service.contract.ts"),
                "import { InjectionToken } from '@angular/core';\n" +
                "export interface IAuthService { login(user: string): boolean; }\n" +
                "export const AUTH = new InjectionToken<IAuthService>('AUTH');\n");
            File.WriteAllText(Path.Combine(folder, "auth.service.ts"),
                "import { Injectable } from '@angular/core';\n" +
                "import { IAuthService } from './auth.service.contract';\n" +
                "@Injectable({ providedIn: 'root' })\n" +
                "export class AuthService implements IAuthService " +
                "{ login(user: string): boolean { return true; } }\n");

            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--folder", folder }, console);

            Assert.Equal(0, exit);
            var doc = File.ReadAllText(Path.Combine(folder, "API.md"), Encoding.UTF8);
            // The document is named after the folder and scoped to its single service.
            Assert.Contains("# auth — Public API", doc);
            Assert.Contains("### `IAuthService`", doc);
            Assert.Contains("| `login` | `user: string` | `boolean` | no | – |", doc);
            Assert.Contains("| `AUTH` | `IAuthService` |", doc);
            // The implementation class is hidden behind its token, just as in workspace mode.
            Assert.DoesNotContain("### `AuthService`", doc);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Folder_mode_scans_subfolders_recursively()
    {
        var ws = NewWorkspace();
        try
        {
            var folder = Path.Combine(ws, "domain");
            var nested = Path.Combine(folder, "models", "core");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "status.ts"),
                "export enum Status { Active, Archived }\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--folder", folder }, new TestConsole());

            Assert.Equal(0, exit);
            var doc = File.ReadAllText(Path.Combine(folder, "API.md"), Encoding.UTF8);
            Assert.Contains("### `Status`", doc);
            Assert.Contains("| `Active` | `0` | no | – |", doc);
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
        Directory.CreateDirectory(ws);
        try
        {
            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--folder", Path.Combine(ws, "nope") }, console);

            Assert.Equal(2, exit);
            Assert.Contains("does not exist", console.Error.ToString());
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Folder_mode_honors_services_filter_and_output_default()
    {
        var ws = NewWorkspace();
        try
        {
            var folder = Path.Combine(ws, "auth");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "auth.ts"),
                "import { Injectable, InjectionToken } from '@angular/core';\n" +
                "export interface IAuthService { login(): boolean; }\n" +
                "export const AUTH = new InjectionToken<IAuthService>('AUTH');\n" +
                "@Injectable()\n" +
                "export class AuthService implements IAuthService { login(): boolean { return true; } }\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--folder", folder, "--services" }, new TestConsole());

            Assert.Equal(0, exit);
            // --services writes SERVICE_API.md at the folder root, not API.md.
            Assert.True(File.Exists(Path.Combine(folder, "SERVICE_API.md")));
            Assert.False(File.Exists(Path.Combine(folder, "API.md")));
            var doc = File.ReadAllText(Path.Combine(folder, "SERVICE_API.md"), Encoding.UTF8);
            Assert.Contains("### `IAuthService`", doc);
            Assert.DoesNotContain("### `AuthService`", doc);
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
            var folder = Path.Combine(ws, "domain");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "lib.ts"),
                "export interface A { b(): void; c: number; }\n" +
                "export type Id = string;\n" +
                "export enum E { X, Y }\n");
            var path = Path.Combine(folder, "API.md");

            await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--folder", folder }, new TestConsole());
            var first = File.ReadAllText(path, Encoding.UTF8);
            await Program.BuildRootCommand()
                .InvokeAsync(new[] { "docs", "--folder", folder }, new TestConsole());
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
