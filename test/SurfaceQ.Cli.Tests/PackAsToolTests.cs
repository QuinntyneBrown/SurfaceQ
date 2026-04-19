// Acceptance Test
// Traces to: L2-014
// Description: SurfaceQ.Cli packs as a .NET global tool and 'surfaceq --version' runs from the installed tool.

using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class PackAsToolTests
{
    [Fact]
    public void Packs_installs_and_runs_version()
    {
        var repoRoot = FindRepoRoot();
        var cliCsproj = Path.Combine(repoRoot, "src", "SurfaceQ.Cli", "SurfaceQ.Cli.csproj");

        var scratch = Path.Combine(Path.GetTempPath(), "sq-tool-" + Guid.NewGuid().ToString("N"));
        var nupkgDir = Path.Combine(scratch, "pkg");
        var toolDir = Path.Combine(scratch, "tool");
        Directory.CreateDirectory(nupkgDir);
        try
        {
            RunDotnet($"pack \"{cliCsproj}\" --nologo -c Release -o \"{nupkgDir}\"", repoRoot);
            RunDotnet($"tool install --tool-path \"{toolDir}\" --add-source \"{nupkgDir}\" SurfaceQ", repoRoot);

            var exeName = OperatingSystem.IsWindows() ? "surfaceq.exe" : "surfaceq";
            var exePath = Path.Combine(toolDir, exeName);
            Assert.True(File.Exists(exePath), $"installed tool binary not found at {exePath}");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("failed to start installed surfaceq");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);

            Assert.True(process.HasExited, "surfaceq --version did not exit within 30s");
            Assert.Equal(0, process.ExitCode);
            Assert.Matches(new Regex(@"\d+\.\d+\.\d+"), stdout.Trim());
            Assert.Equal(string.Empty, stderr.Trim());
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
        throw new DirectoryNotFoundException("could not locate SurfaceQ.sln walking upward from " + AppContext.BaseDirectory);
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
            throw new TimeoutException($"'dotnet {arguments}' did not exit within 180s\nstdout:\n{stdout}\nstderr:\n{stderr}");
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
