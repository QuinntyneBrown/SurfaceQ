// Acceptance Test
// Traces to: L2-021
// Description: `surfaceq --version` prints a semver and exits 0.

using System.CommandLine;
using System.CommandLine.IO;
using System.Text.RegularExpressions;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class VersionTests
{
    [Fact]
    public async Task Version_flag_prints_semver_and_exits_zero()
    {
        var root = Program.BuildRootCommand();
        var console = new TestConsole();

        var exitCode = await root.InvokeAsync(new[] { "--version" }, console);

        Assert.Equal(0, exitCode);
        var output = console.Out.ToString()!.Trim();
        Assert.Matches(new Regex(@"^\d+\.\d+\.\d+"), output);
    }
}
