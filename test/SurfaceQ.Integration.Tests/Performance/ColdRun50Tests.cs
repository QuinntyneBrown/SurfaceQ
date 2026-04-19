// Acceptance Test
// Traces to: L2-015
// Description: A fresh CLI process generates 50-file fixture in under 5 seconds (cold).

using System.Diagnostics;
using Xunit;

namespace SurfaceQ.Integration.Tests.Performance;

public class ColdRun50Tests
{
    [Fact]
    [Trait("Category", "Performance")]
    public void Cold_generate_of_50_files_completes_under_5_seconds()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-perf-cold-" + Guid.NewGuid().ToString("N"));
        try
        {
            PerformanceHelpers.WriteFixture(dir, count: 50);

            var psi = PerformanceHelpers.BuildGenerateStartInfo(PerformanceHelpers.FindCliDll(), dir);
            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("failed to start CLI");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);
            stopwatch.Stop();

            Assert.True(process.HasExited, "CLI did not exit within 30s");
            Assert.Equal(0, process.ExitCode);
            Assert.True(File.Exists(Path.Combine(dir, "src", "public-api.ts")));
            Assert.True(
                stopwatch.Elapsed.TotalSeconds < 5.0,
                $"50-file cold generate exceeded 5s ({stopwatch.Elapsed.TotalSeconds:F2}s)\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
