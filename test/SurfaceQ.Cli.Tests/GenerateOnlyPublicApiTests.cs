// Acceptance Test
// Traces to: L2-045
// Description: generate --only-public-api keeps only @publicApi-tagged declarations
// (kind-agnostic, re-exports resolved at the declaration site, empty when nothing is tagged)
// and leaves the default generate behavior unchanged.

using System.CommandLine;
using System.CommandLine.IO;
using System.Text;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateOnlyPublicApiTests
{
    private const string Contracts =
        "import { InjectionToken } from '@angular/core';\n" +
        "\n" +
        "/** @publicApi */\n" +
        "export interface IBill { id: string; }\n" +
        "\n" +
        "export interface IHidden { id: string; }\n" +
        "\n" +
        "/** @publicApi */\n" +
        "export type BillStatus = 'open' | 'paid';\n" +
        "\n" +
        "export type HiddenAlias = string;\n" +
        "\n" +
        "/** @publicApi */\n" +
        "export enum BillKind { Utility, Rent }\n" +
        "\n" +
        "export enum HiddenKind { A }\n" +
        "\n" +
        "/** @publicApi */\n" +
        "export class BillModel {}\n" +
        "\n" +
        "export class HiddenModel {}\n" +
        "\n" +
        "/** @publicApi */\n" +
        "export function formatBill(): string { return ''; }\n" +
        "\n" +
        "export function hiddenFn(): void {}\n" +
        "\n" +
        "/** @publicApi */\n" +
        "export const BILLS_TOKEN = new InjectionToken<IBill>('BILLS_TOKEN');\n" +
        "\n" +
        "export const HIDDEN_CONST = 1;\n";

    [Fact]
    public async Task Flag_keeps_only_tagged_declarations_of_every_kind()
    {
        var dir = CreateProject();
        try
        {
            File.WriteAllText(Path.Combine(dir, "src", "contracts.ts"), Contracts);

            var exitCode = await Invoke(dir, "generate", "--only-public-api");

            Assert.Equal(0, exitCode);
            var expected =
                "export { BillKind, BillModel, formatBill, BILLS_TOKEN } from './contracts';\n" +
                "export type { IBill, BillStatus } from './contracts';\n";
            Assert.Equal(expected, ReadEntry(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Without_flag_every_export_appears_unchanged()
    {
        var dir = CreateProject();
        try
        {
            File.WriteAllText(Path.Combine(dir, "src", "contracts.ts"), Contracts);

            var exitCode = await Invoke(dir, "generate");

            Assert.Equal(0, exitCode);
            var expected =
                "export { BillKind, HiddenKind, BillModel, HiddenModel, formatBill, hiddenFn, " +
                "BILLS_TOKEN, HIDDEN_CONST } from './contracts';\n" +
                "export type { IBill, IHidden, BillStatus, HiddenAlias } from './contracts';\n";
            Assert.Equal(expected, ReadEntry(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Reexports_follow_the_tag_at_the_declaration_site()
    {
        var dir = CreateProject();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "src", "lib"));
            File.WriteAllText(
                Path.Combine(dir, "src", "lib", "x.ts"),
                "/** @publicApi */\n" +
                "export interface X { id: string; }\n" +
                "\n" +
                "export interface Z { id: string; }\n");
            File.WriteAllText(
                Path.Combine(dir, "src", "barrel.ts"),
                "/** @publicApi */\n" +
                "const PUB_LOCAL = 1;\n" +
                "const HIDDEN_LOCAL = 2;\n" +
                "export { PUB_LOCAL, HIDDEN_LOCAL };\n" +
                "export type { X as PublicX, Z as PublicZ } from './lib/x';\n");

            var exitCode = await Invoke(dir, "generate", "--only-public-api");

            Assert.Equal(0, exitCode);
            var expected =
                "export { PUB_LOCAL } from './barrel';\n" +
                "export type { PublicX } from './barrel';\n" +
                "export type { X } from './lib/x';\n";
            Assert.Equal(expected, ReadEntry(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Tag_resolves_through_an_index_barrel_export_star()
    {
        var dir = CreateProject();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, "src", "shared"));
            File.WriteAllText(
                Path.Combine(dir, "src", "shared", "impl.ts"),
                "/** @publicApi */\n" +
                "export interface X { id: string; }\n");
            File.WriteAllText(
                Path.Combine(dir, "src", "shared", "index.ts"),
                "export * from './impl';\n");
            File.WriteAllText(
                Path.Combine(dir, "src", "barrel.ts"),
                "export type { X as PublicX } from './shared';\n");

            var exitCode = await Invoke(dir, "generate", "--only-public-api");

            Assert.Equal(0, exitCode);
            var expected =
                "export type { PublicX } from './barrel';\n" +
                "export type { X } from './shared/impl';\n";
            Assert.Equal(expected, ReadEntry(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Diagnostic_verbosity_traces_each_exclusion()
    {
        var dir = CreateProject();
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "src", "a.ts"),
                "/** @publicApi */\n" +
                "export class A {}\n" +
                "\n" +
                "export class B {}\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();
            var exitCode = await root.InvokeAsync(
                new[] { "generate", "--only-public-api", "--verbosity", "diagnostic", "--project", dir },
                console);

            Assert.Equal(0, exitCode);
            Assert.Contains(
                "trace: only-public-api excluded 'B' from 'src/a.ts'",
                console.Out.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Nothing_tagged_writes_empty_output_and_exits_zero()
    {
        var dir = CreateProject();
        try
        {
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");

            var exitCode = await Invoke(dir, "generate", "--only-public-api");

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, ReadEntry(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(
            Path.Combine(dir, "ng-package.json"),
            "{ \"entryFile\": \"src/public-api.ts\" }");
        return dir;
    }

    private static async Task<int> Invoke(string dir, params string[] args)
    {
        var root = Program.BuildRootCommand();
        var console = new TestConsole();
        var full = new List<string>(args) { "--project", dir };
        return await root.InvokeAsync(full.ToArray(), console);
    }

    private static string ReadEntry(string dir)
    {
        return File.ReadAllText(Path.Combine(dir, "src", "public-api.ts"), Encoding.UTF8);
    }
}
