// Acceptance Test
// Traces to: L2-003
// Description: diff exits 0 with empty stdout when on-disk matches rendered output.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class DiffEqualTests
{
    [Fact]
    public async Task Diff_exits_0_with_empty_stdout_when_equal()
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

            var diffConsole = new TestConsole();
            var diffExit = await root.InvokeAsync(new[] { "diff", "--project", dir }, diffConsole);

            Assert.Equal(0, diffExit);
            Assert.Equal(string.Empty, diffConsole.Out.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
