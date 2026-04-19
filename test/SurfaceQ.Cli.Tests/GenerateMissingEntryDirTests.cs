// Acceptance Test
// Traces to: L2-005
// Description: Missing entryFile directory causes exit 2 with stderr identifying the directory.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateMissingEntryDirTests
{
    [Fact]
    public async Task Missing_entryFile_directory_exits_2()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ \"entryFile\": \"nonexistent/public-api.ts\" }");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(2, exitCode);
            var err = console.Error.ToString();
            Assert.Contains("nonexistent", err);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
