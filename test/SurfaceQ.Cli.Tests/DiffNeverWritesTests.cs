// Acceptance Test
// Traces to: L2-003
// Description: diff never modifies public-api.ts in either the equal or differing scenarios.

using System.CommandLine;
using System.CommandLine.IO;
using System.Security.Cryptography;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class DiffNeverWritesTests
{
    [Fact]
    public async Task Diff_does_not_modify_file_on_match()
    {
        await AssertDiffLeavesFileUntouched(tamper: false);
    }

    [Fact]
    public async Task Diff_does_not_modify_file_on_mismatch()
    {
        await AssertDiffLeavesFileUntouched(tamper: true);
    }

    private static async Task AssertDiffLeavesFileUntouched(bool tamper)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");

            var root = Program.BuildRootCommand();
            var genConsole = new TestConsole();
            Assert.Equal(0, await root.InvokeAsync(new[] { "generate", "--project", dir }, genConsole));

            var outputPath = Path.Combine(dir, "src", "public-api.ts");
            if (tamper)
            {
                File.AppendAllText(outputPath, "// tampered\n");
            }

            var mtimeBefore = File.GetLastWriteTimeUtc(outputPath);
            var hashBefore = Hash(outputPath);
            await Task.Delay(50);

            var diffConsole = new TestConsole();
            await root.InvokeAsync(new[] { "diff", "--project", dir }, diffConsole);

            Assert.Equal(mtimeBefore, File.GetLastWriteTimeUtc(outputPath));
            Assert.Equal(hashBefore, Hash(outputPath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string Hash(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}
