// Acceptance Test
// Traces to: L2-002
// Description: check exits 0 when on-disk public-api.ts matches rendered output and does not write.

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class CheckMatchTests
{
    [Fact]
    public async Task Check_exits_0_and_does_not_touch_file_when_match()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");

            var root = Program.BuildRootCommand();
            var genConsole = new TestConsole();
            var genExit = await root.InvokeAsync(new[] { "generate", "--project", dir }, genConsole);
            Assert.Equal(0, genExit);

            var outputPath = Path.Combine(dir, "src", "public-api.ts");
            var mtimeBefore = File.GetLastWriteTimeUtc(outputPath);
            await Task.Delay(50);

            var checkConsole = new TestConsole();
            var checkExit = await root.InvokeAsync(new[] { "check", "--project", dir }, checkConsole);

            Assert.Equal(0, checkExit);
            var mtimeAfter = File.GetLastWriteTimeUtc(outputPath);
            Assert.Equal(mtimeBefore, mtimeAfter);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
