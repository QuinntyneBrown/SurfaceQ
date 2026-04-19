// Acceptance Test
// Traces to: L2-016
// Description: Info logs go to stdout only; warnings go to stderr only.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class LoggingStreamsTests
{
    [Fact]
    public async Task Info_goes_to_stdout_and_warning_goes_to_stderr()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{}");
            File.WriteAllText(Path.Combine(dir, "src", "bad.ts"), "export default class Foo {}\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
            var stdout = console.Out.ToString()!;
            var stderr = console.Error.ToString()!;

            Assert.Contains("info:", stdout);
            Assert.DoesNotContain("info:", stderr);

            Assert.Contains("warn:", stderr);
            Assert.DoesNotContain("warn:", stdout);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
