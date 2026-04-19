// Acceptance Test
// Traces to: L2-005
// Description: Malformed ng-package.json causes exit 2 with JSON error.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateMalformedManifestTests
{
    [Fact]
    public async Task Malformed_manifest_exits_2()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ this is not json");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(2, exitCode);
            var err = console.Error.ToString();
            Assert.Contains(manifest, err);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
