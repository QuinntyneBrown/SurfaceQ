// Acceptance Test
// Traces to: L2-003
// Description: diff exits 1 and prints a unified diff with '---'/'+++' headers and '+<added>' lines.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class DiffUnifiedTests
{
    [Fact]
    public async Task Diff_exits_1_with_unified_diff_on_mismatch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");

            var root = Program.BuildRootCommand();
            var genConsole = new TestConsole();
            Assert.Equal(0, await root.InvokeAsync(new[] { "generate", "--project", dir }, genConsole));

            var outputPath = Path.Combine(dir, "src", "public-api.ts");
            var original = File.ReadAllText(outputPath);
            var tampered = original + "// stray inserted line\n";
            File.WriteAllText(outputPath, tampered);

            var diffConsole = new TestConsole();
            var diffExit = await root.InvokeAsync(new[] { "diff", "--project", dir }, diffConsole);

            Assert.Equal(1, diffExit);
            var stdout = diffConsole.Out.ToString()!;
            Assert.Contains("---", stdout);
            Assert.Contains("+++", stdout);
            Assert.Contains("+// stray inserted line", stdout);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
