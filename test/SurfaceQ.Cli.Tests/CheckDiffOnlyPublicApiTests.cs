// Acceptance Test
// Traces to: L2-045
// Description: check/diff --only-public-api accept an on-disk file matching the
// filtered output (exit 0), while the unflagged commands report it as drift (exit 1).

using System.CommandLine;
using System.CommandLine.IO;
using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class CheckDiffOnlyPublicApiTests
{
    [Theory]
    [InlineData("check")]
    [InlineData("diff")]
    public async Task Flag_accepts_filtered_file_while_unflagged_reports_drift(string command)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "ng-package.json"),
                "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(
                Path.Combine(dir, "src", "a.ts"),
                "/** @publicApi */\n" +
                "export class A {}\n" +
                "\n" +
                "export class B {}\n");
            File.WriteAllText(
                Path.Combine(dir, "src", "public-api.ts"),
                "export { A } from './a';\n");

            var withFlag = await Invoke(command, "--only-public-api", "--project", dir);
            var withoutFlag = await Invoke(command, "--project", dir);

            Assert.Equal(0, withFlag);
            Assert.Equal(1, withoutFlag);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("check")]
    [InlineData("diff")]
    public async Task Flag_reports_drift_when_file_still_contains_untagged_export(string command)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "ng-package.json"),
                "{ \"entryFile\": \"src/public-api.ts\" }");
            File.WriteAllText(
                Path.Combine(dir, "src", "a.ts"),
                "/** @publicApi */\n" +
                "export class A {}\n" +
                "\n" +
                "export class B {}\n");
            File.WriteAllText(
                Path.Combine(dir, "src", "public-api.ts"),
                "export { A, B } from './a';\n");

            var exitCode = await Invoke(command, "--only-public-api", "--project", dir);

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task<int> Invoke(params string[] args)
    {
        var root = Program.BuildRootCommand();
        var console = new TestConsole();
        return await root.InvokeAsync(args, console);
    }
}
