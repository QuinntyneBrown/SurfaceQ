// Acceptance Test
// Traces to: L2-015
// Description: A 500-file fixture runs with peak CLI working set under 512 MB.

using System.Diagnostics;
using Xunit;

namespace SurfaceQ.Integration.Tests.Performance;

public class MemoryCeiling500Tests
{
    private const long CeilingBytes = 512L * 1024 * 1024;

    [Fact]
    [Trait("Category", "Performance")]
    public void Generate_of_500_files_stays_under_512_megabytes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-perf-mem-" + Guid.NewGuid().ToString("N"));
        try
        {
            PerformanceHelpers.WriteFixture(dir, count: 500);

            var psi = PerformanceHelpers.BuildGenerateStartInfo(PerformanceHelpers.FindCliDll(), dir);
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("failed to start CLI");

            long peakBytes = 0;
            while (!process.HasExited)
            {
                try
                {
                    process.Refresh();
                    peakBytes = Math.Max(peakBytes, process.WorkingSet64);
                }
                catch
                {
                    // race: process exited between HasExited check and Refresh()
                }
                Thread.Sleep(50);
            }
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(60_000);

            Assert.Equal(0, process.ExitCode);
            Assert.True(
                peakBytes < CeilingBytes,
                $"500-file generate peak working set was {peakBytes / (1024.0 * 1024.0):F1} MiB, exceeding the 512 MiB ceiling\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
