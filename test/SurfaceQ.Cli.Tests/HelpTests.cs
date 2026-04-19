// Acceptance Test
// Traces to: L2-021
// Description: `surfaceq --help` and `-h` list generate/check/diff and exit 0.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class HelpTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Help_lists_all_three_subcommands(string flag)
    {
        var root = Program.BuildRootCommand();
        var console = new TestConsole();

        var exitCode = await root.InvokeAsync(new[] { flag }, console);

        Assert.Equal(0, exitCode);
        var output = console.Out.ToString()!;
        Assert.Contains("generate", output);
        Assert.Contains("check", output);
        Assert.Contains("diff", output);
    }
}
