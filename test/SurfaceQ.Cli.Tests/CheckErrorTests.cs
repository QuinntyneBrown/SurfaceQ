// Acceptance Test
// Traces to: L2-002
// Description: check exits 2 when the pipeline surfaces an execution error (e.g. missing manifest).

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class CheckErrorTests
{
    [Fact]
    public async Task Missing_manifest_exits_2_with_stderr_message()
    {
        var empty = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "check", "--project", empty }, console);

            Assert.Equal(2, exitCode);
            Assert.Contains(empty, console.Error.ToString());
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }
}
