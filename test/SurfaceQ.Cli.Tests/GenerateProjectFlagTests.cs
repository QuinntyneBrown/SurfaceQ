// Acceptance Test
// Traces to: L2-001
// Description: --project accepts either a directory or the path to ng-package.json.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateProjectFlagTests
{
    [Fact]
    public async Task Project_flag_accepts_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(dir, "src", "public-api.ts")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Project_flag_accepts_ng_package_json_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", manifest }, console);

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(Path.Combine(dir, "src", "public-api.ts")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
