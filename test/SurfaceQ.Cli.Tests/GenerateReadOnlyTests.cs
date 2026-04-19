// Acceptance Test
// Traces to: L2-019
// Description: Read-only public-api.ts causes generate to exit 2 and leaves the file unchanged.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateReadOnlyTests
{
    [Fact]
    public async Task ReadOnly_output_exits_2_and_preserves_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        var outputPath = Path.Combine(dir, "src", "public-api.ts");
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");
            File.WriteAllText(outputPath, "// locked\n");
            File.SetAttributes(outputPath, FileAttributes.ReadOnly);

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(2, exitCode);
            var err = console.Error.ToString()!.ToLowerInvariant();
            Assert.True(
                err.Contains("permission") || err.Contains("read-only") || err.Contains("readonly") || err.Contains("access"),
                $"stderr should mention permission/read-only/access — got: {console.Error}");
            var preserved = File.ReadAllText(outputPath);
            Assert.Equal("// locked\n", preserved);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.SetAttributes(outputPath, FileAttributes.Normal);
            }
            Directory.Delete(dir, recursive: true);
        }
    }
}
