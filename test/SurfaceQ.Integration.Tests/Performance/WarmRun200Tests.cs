// Acceptance Test
// Traces to: L2-015
// Description: A 200-file fixture completes warm generate in under 10 seconds.

using System.Diagnostics;
using Xunit;

namespace SurfaceQ.Integration.Tests.Performance;

public class WarmRun200Tests
{
    [Fact]
    [Trait("Category", "Performance")]
    public void Warm_generate_of_200_files_completes_under_10_seconds()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-perf-warm-" + Guid.NewGuid().ToString("N"));
        try
        {
            PerformanceHelpers.WriteFixture(dir, count: 200);
            var cliDll = PerformanceHelpers.FindCliDll();

            RunOnce(cliDll, dir);
            Assert.True(File.Exists(Path.Combine(dir, "src", "public-api.ts")));

            var stopwatch = Stopwatch.StartNew();
            RunOnce(cliDll, dir);
            stopwatch.Stop();

            Assert.True(
                stopwatch.Elapsed.TotalSeconds < 10.0,
                $"200-file warm generate exceeded 10s ({stopwatch.Elapsed.TotalSeconds:F2}s)");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static void RunOnce(string cliDll, string projectDir)
    {
        var psi = PerformanceHelpers.BuildGenerateStartInfo(cliDll, projectDir);
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("failed to start CLI");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(60_000);
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"CLI did not exit within 60s\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"CLI failed (exit {process.ExitCode})\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }
    }
}
