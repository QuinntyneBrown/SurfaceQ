// Acceptance Test
// Traces to: L2-016
// Description: --verbosity quiet suppresses stdout on success; --verbosity diagnostic emits trace output.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class VerbosityTests
{
    [Fact]
    public async Task Quiet_verbosity_suppresses_stdout_on_success()
    {
        var dir = CreateCleanFixture();
        try
        {
            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir, "--verbosity", "quiet" }, console);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, console.Out.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Diagnostic_verbosity_emits_trace_lines_to_stdout()
    {
        var dir = CreateCleanFixture();
        try
        {
            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir, "--verbosity", "diagnostic" }, console);

            Assert.Equal(0, exitCode);
            var stdout = console.Out.ToString()!;
            var mentionsSidecar = stdout.Contains("sidecar", StringComparison.OrdinalIgnoreCase);
            var mentionsWalker = stdout.Contains("walker", StringComparison.OrdinalIgnoreCase);
            Assert.True(
                mentionsSidecar || mentionsWalker,
                $"diagnostic stdout should mention 'sidecar' or 'walker'; got:\n{stdout}");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateCleanFixture()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
        File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");
        return dir;
    }
}
