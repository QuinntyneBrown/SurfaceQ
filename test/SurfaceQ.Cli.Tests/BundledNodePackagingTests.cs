// Acceptance Test
// Traces to: L2-014
// Description: The packed nupkg contains Node placeholder entries for all six RID folders.

using System.Diagnostics;
using System.IO.Compression;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class BundledNodePackagingTests
{
    private static readonly string[] RequiredRids =
    {
        "win-x64", "win-arm64",
        "linux-x64", "linux-arm64",
        "osx-x64", "osx-arm64",
    };

    [Fact]
    public void Nupkg_contains_all_six_rid_node_entries()
    {
        var repoRoot = FindRepoRoot();
        var cliCsproj = Path.Combine(repoRoot, "src", "SurfaceQ.Cli", "SurfaceQ.Cli.csproj");
        var scratch = Path.Combine(Path.GetTempPath(), "sq-pack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            RunDotnet($"pack \"{cliCsproj}\" --nologo -c Release -o \"{scratch}\"", repoRoot);
            var nupkg = Directory.EnumerateFiles(scratch, "SurfaceQ.*.nupkg").FirstOrDefault();
            Assert.NotNull(nupkg);

            using var zip = ZipFile.OpenRead(nupkg!);
            var entries = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
            foreach (var rid in RequiredRids)
            {
                var isWindowsRid = rid.StartsWith("win-", StringComparison.Ordinal);
                var binary = isWindowsRid ? "node.exe" : "node";
                var needle = $"content/node/{rid}/{binary}";
                Assert.True(
                    entries.Any(e => e.EndsWith(needle, StringComparison.Ordinal)),
                    $"nupkg is missing entry for RID '{rid}' ({needle})\nentries:\n" + string.Join("\n", entries));
            }
        }
        finally
        {
            TryDeleteRecursive(scratch);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SurfaceQ.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("SurfaceQ.sln not found walking up from " + AppContext.BaseDirectory);
    }

    private static void RunDotnet(string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start dotnet " + arguments);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(180_000);
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"'dotnet {arguments}' timed out\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet {arguments}' failed (exit {process.ExitCode})\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }
    }

    private static void TryDeleteRecursive(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
