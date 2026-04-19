// Acceptance Test
// Traces to: L2-001
// Description: generate overwrites any pre-existing public-api.ts.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateOverwritesTests
{
    [Fact]
    public async Task Existing_public_api_is_overwritten()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ \"entryFile\": \"src/public-api.ts\" }");
            var outputPath = Path.Combine(dir, "src", "public-api.ts");
            File.WriteAllText(outputPath, "// stale\n");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
            var actual = File.ReadAllText(outputPath);
            Assert.DoesNotContain("// stale", actual);
            Assert.Contains("export { A } from './a';", actual);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
