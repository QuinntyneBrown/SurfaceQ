// Acceptance Test
// Traces to: L2-014
// Description: The packed nupkg contains the pinned typescript package.json under content/sidecar/node_modules.

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class BundledTypescriptPackagingTests
{
    [Fact]
    public void Nupkg_contains_pinned_typescript_package_json()
    {
        var repoRoot = FindRepoRoot();
        var expectedVersion = ReadPinnedVersion(repoRoot);
        var cliCsproj = Path.Combine(repoRoot, "src", "SurfaceQ.Cli", "SurfaceQ.Cli.csproj");

        var scratch = Path.Combine(Path.GetTempPath(), "sq-pack-ts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            RunDotnet($"pack \"{cliCsproj}\" --nologo -c Release -o \"{scratch}\"", repoRoot);
            var nupkg = Directory.EnumerateFiles(scratch, "SurfaceQ.*.nupkg").FirstOrDefault();
            Assert.NotNull(nupkg);

            using var zip = ZipFile.OpenRead(nupkg!);
            var entry = zip.Entries.FirstOrDefault(e =>
                e.FullName.Replace('\\', '/').EndsWith("content/sidecar/node_modules/typescript/package.json", StringComparison.Ordinal));
            Assert.True(entry != null, "nupkg missing content/sidecar/node_modules/typescript/package.json");

            using var stream = entry!.Open();
            using var doc = JsonDocument.Parse(stream);
            var actualVersion = doc.RootElement.GetProperty("version").GetString();
            Assert.Equal(expectedVersion, actualVersion);
        }
        finally
        {
            TryDeleteRecursive(scratch);
        }
    }

    private static string ReadPinnedVersion(string repoRoot)
    {
        var sidecarPackageJson = Path.Combine(repoRoot, "src", "SurfaceQ.Sidecar.Node", "package.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(sidecarPackageJson));
        return doc.RootElement.GetProperty("dependencies").GetProperty("typescript").GetString()!;
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
        process.WaitForExit(240_000);
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
