// Acceptance Test
// Traces to: L2-001
// Description: generate writes a byte-exact public-api.ts for a known fixture and exits 0.

using System.CommandLine;
using System.CommandLine.IO;
using System.Text;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateWritesFileTests
{
    [Fact]
    public async Task Generate_writes_exact_public_api_for_two_source_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");
            File.WriteAllText(Path.Combine(dir, "src", "b.ts"), "export const B = 1;\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
            var outputPath = Path.Combine(dir, "src", "public-api.ts");
            Assert.True(File.Exists(outputPath));
            var actual = File.ReadAllText(outputPath, Encoding.UTF8);
            var expected =
                "// ============================================================\n" +
                "// SurfaceQ — generated public API. DO NOT EDIT.\n" +
                "// Regenerate with `surfaceq generate`.\n" +
                "// ============================================================\n" +
                "export { A } from './a';\n" +
                "export { B } from './b';\n";
            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
