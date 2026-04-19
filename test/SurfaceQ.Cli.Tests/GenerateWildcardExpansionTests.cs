// Acceptance Test
// Traces to: L2-009
// Description: 'export * from ./inner' expands to explicit named re-exports in public-api.ts.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateWildcardExpansionTests
{
    [Fact]
    public async Task Wildcard_export_is_expanded_to_explicit_named_exports()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export * from './inner';\n");
            File.WriteAllText(
                Path.Combine(dir, "src", "inner.ts"),
                "export const A = 1;\nexport const B = 2;\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
            var output = File.ReadAllText(Path.Combine(dir, "src", "public-api.ts"));
            Assert.Contains("export { A, B } from './inner';", output);
            Assert.DoesNotContain("export *", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
