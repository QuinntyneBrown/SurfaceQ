// Acceptance Test
// Traces to: L2-016
// Description: Log output is plain text — no ANSI escape sequences and no JSON framing.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class PlainTextLogsTests
{
    [Fact]
    public async Task Logs_contain_no_ansi_escapes_and_are_not_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(
                new[] { "generate", "--project", dir, "--verbosity", "detailed" },
                console);

            Assert.Equal(0, exitCode);
            var combined = console.Out.ToString() + console.Error.ToString();
            Assert.DoesNotContain("\u001b[", combined);
            foreach (var line in combined.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Assert.False(
                    line.TrimStart().StartsWith("{"),
                    $"log line should not be JSON-framed; got: {line}");
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
