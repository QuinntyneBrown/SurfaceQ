// Acceptance Test
// Traces to: L2-018
// Description: A TypeScript syntax error causes generate to exit 2 with file and line in stderr.

using System.CommandLine;
using System.CommandLine.IO;
using System.Text.RegularExpressions;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class GenerateSyntaxErrorTests
{
    [Fact]
    public async Task Syntax_error_exits_2_and_stderr_identifies_file_and_line()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "broken.ts"), "export class {\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(2, exitCode);
            var err = console.Error.ToString();
            Assert.Contains("broken.ts", err);
            Assert.Matches(new Regex(@"line\s*\d+"), err);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
