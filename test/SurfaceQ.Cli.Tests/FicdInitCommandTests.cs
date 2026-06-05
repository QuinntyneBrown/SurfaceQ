// Acceptance Test
// Traces to: L2-039
// Description: `surfaceq ficd-init` seeds a starter ficd/ folder into each library
// (never overwriting existing files), exits 2 when no library is found, and the
// seeded scaffold is consumable by `surfaceq ficd` end to end.

using System.Text;
using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class FicdInitCommandTests
{
    [Fact]
    public async Task Ficd_init_seeds_a_starter_ficd_folder_into_each_library()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "widget");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd-init", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);
            var ficdDir = Path.Combine(ws, "libs", "widget", "ficd");
            Assert.True(File.Exists(Path.Combine(ficdDir, "ficd.yml")));
            Assert.True(File.Exists(Path.Combine(ficdDir, "introduction.md")));
            Assert.True(File.Exists(Path.Combine(
                ficdDir, "detailed-interface-definition", "widget-interface", "core-api", "services.md")));

            var services = File.ReadAllText(Path.Combine(
                ficdDir, "detailed-interface-definition", "widget-interface", "core-api", "services.md"));
            Assert.Contains("<!-- add services here", services);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_init_never_overwrites_an_existing_file()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "widget");
            var ficdDir = Path.Combine(ws, "libs", "widget", "ficd");
            Directory.CreateDirectory(ficdDir);
            File.WriteAllText(Path.Combine(ficdDir, "ficd.yml"), "documentTitle: KEEP ME\n");

            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd-init", "--project", ws }, new TestConsole());

            Assert.Equal(0, exit);
            // The pre-existing manifest is untouched...
            Assert.Equal("documentTitle: KEEP ME\n", File.ReadAllText(Path.Combine(ficdDir, "ficd.yml")));
            // ...but the missing section files are still seeded.
            Assert.True(File.Exists(Path.Combine(ficdDir, "introduction.md")));
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Ficd_init_exits_2_when_no_library_is_found()
    {
        var ws = NewWorkspace();
        try
        {
            var console = new TestConsole();
            var exit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd-init", "--project", Path.Combine(ws, "empty") }, console);

            Assert.Equal(2, exit);
            Assert.Contains("no ng-package.json", console.Error.ToString());
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public async Task Seeded_scaffold_is_consumable_by_ficd_end_to_end()
    {
        var ws = NewWorkspace();
        try
        {
            CreateLibrary(ws, "widget");

            var initExit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd-init", "--project", ws }, new TestConsole());
            Assert.Equal(0, initExit);

            // The seeded (un-edited) scaffold renders without error.
            var ficdExit = await Program.BuildRootCommand()
                .InvokeAsync(new[] { "ficd", "--project", ws }, new TestConsole());
            Assert.Equal(0, ficdExit);

            var readme = File.ReadAllText(
                Path.Combine(ws, "libs", "widget", "docs", "ficd", "README.md"), Encoding.UTF8);
            Assert.Contains("## Document Set", readme);
            Assert.Contains("[Core API — Services]", readme);
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

    private static void CreateLibrary(string ws, string name)
    {
        var libDir = Path.Combine(ws, "libs", name);
        var srcDir = Path.Combine(libDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(libDir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
        File.WriteAllText(Path.Combine(srcDir, "public-api.ts"), "export * from './widget';\n");
        File.WriteAllText(Path.Combine(srcDir, "widget.ts"), "export interface Widget { id: string; }\n");
    }
}
