// Acceptance Test
// Traces to: L2-020
// Description: When two files each export a symbol named Foo, both export statements appear.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class NameCollisionsTests
{
    [Fact]
    public async Task Both_files_emit_their_Foo_export()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class Foo {}\n");
            File.WriteAllText(Path.Combine(dir, "src", "b.ts"), "export class Foo {}\n");

            var root = Program.BuildRootCommand();
            var console = new TestConsole();

            var exitCode = await root.InvokeAsync(new[] { "generate", "--project", dir }, console);

            Assert.Equal(0, exitCode);
            var output = File.ReadAllText(Path.Combine(dir, "src", "public-api.ts"));
            Assert.Contains("export { Foo } from './a';", output);
            Assert.Contains("export { Foo } from './b';", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
