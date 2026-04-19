// Acceptance Test
// Traces to: L2-010
// Description: Default exports are skipped with a warning; run exits 0 and writes header-only file.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateDefaultExportTests
{
    [Fact]
    public async Task Default_export_skipped_with_warning_and_exit_0()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "default.ts"), "export default class Foo {}\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
            var outputPath = Path.Combine(dir, "src", "public-api.ts");
            Assert.True(File.Exists(outputPath), "public-api.ts should be written");
            var content = File.ReadAllText(outputPath);
            Assert.StartsWith("// ====", content);
            Assert.DoesNotContain("Foo", content);
            Assert.DoesNotContain("export ", content);
            var err = console.Error.ToString();
            Assert.Contains("warn:", err);
            Assert.Contains("default.ts", err);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
