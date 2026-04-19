// Acceptance Test
// Traces to: L2-004
// Description: generate exits 2 when no ng-package.json is found.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateMissingManifestTests
{
    [Fact]
    public async Task Missing_manifest_exits_2()
    {
        var empty = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", empty }, console);

            Assert.Equal(2, exitCode);
            Assert.Contains(empty, console.Error.ToString());
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }
}
