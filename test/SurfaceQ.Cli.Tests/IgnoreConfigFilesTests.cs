// Acceptance Test
// Traces to: L2-022
// Description: Stray surfaceq.config.json / .surfaceqrc files are ignored — their presence does not fail generate.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class IgnoreConfigFilesTests
{
    [Fact]
    public async Task Invalid_surfaceq_config_json_does_not_affect_generate()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");
            File.WriteAllText(Path.Combine(dir, "surfaceq.config.json"), "{ not valid json");
            File.WriteAllText(Path.Combine(dir, ".surfaceqrc"), "{ also not valid json");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
