// Acceptance Test
// Traces to: L2-012
// Description: Two consecutive generate runs against unchanged source produce byte-identical output.

using System.CommandLine;
using System.CommandLine.IO;
using System.Security.Cryptography;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class DeterminismRepeatTests
{
    [Fact]
    public async Task Repeat_generate_is_byte_identical()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(Path.Combine(dir, "src", "a.ts"), "export class A {}\n");
            File.WriteAllText(Path.Combine(dir, "src", "b.ts"), "export interface B {}\n");
            File.WriteAllText(Path.Combine(dir, "src", "c.ts"), "export const C = 1;\n");

            var root = Program.BuildRootCommand();
            var outputPath = Path.Combine(dir, "src", "public-api.ts");

            Assert.Equal(0, await root.InvokeAsync(new[] { "generate", "--project", dir }, new TestConsole()));
            var firstHash = Hash(outputPath);

            Assert.Equal(0, await root.InvokeAsync(new[] { "generate", "--project", dir }, new TestConsole()));
            var secondHash = Hash(outputPath);

            Assert.Equal(firstHash, secondHash);
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
