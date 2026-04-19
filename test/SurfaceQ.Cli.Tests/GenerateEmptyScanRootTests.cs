// Acceptance Test
// Traces to: L2-006
// Description: Empty scan root writes a header-only public-api.ts and exits 0.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateEmptyScanRootTests
{
    [Fact]
    public async Task Empty_scan_root_writes_header_only_file_and_exits_0()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "index.ts"), "export const I = 1;");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
            var outputPath = Path.Combine(dir, "src", "public-api.ts");
            Assert.True(File.Exists(outputPath), "public-api.ts should be written");
            var output = File.ReadAllText(outputPath);
            Assert.StartsWith("// ====", output);
            Assert.Contains("SurfaceQ", output);
            Assert.Contains("DO NOT EDIT", output);
            Assert.EndsWith("\n", output);
            Assert.False(output.EndsWith("\n\n"), "output must not end with two newlines");
            Assert.DoesNotContain("export ", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
