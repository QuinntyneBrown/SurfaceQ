// Acceptance Test
// Traces to: L2-002
// Description: check detects drift, exits 1, prints a concise message, and does not emit diff markers.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class CheckDriftTests
{
    [Fact]
    public async Task Check_exits_1_with_concise_drift_message()
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
            File.AppendAllText(outputPath, "   \n");

            var checkConsole = new TestConsole();
            var checkExit = await root.InvokeAsync(new[] { "check", "--project", dir }, checkConsole);

            Assert.Equal(1, checkExit);
            var stdout = checkConsole.Out.ToString()!;
            var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines);
            Assert.Contains("drift", lines[0], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@@", stdout);
            Assert.DoesNotContain("+++", stdout);
            Assert.DoesNotContain("---", stdout);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
