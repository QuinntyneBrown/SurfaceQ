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
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "ng-package.json"),
                "{ \"entryFile\": \"src/public-api.ts\" }");
            WriteFixtureFiles(dir, count: 50);

            var cliDll = FindCliDll();
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(cliDll);
            psi.ArgumentList.Add("generate");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(dir);

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

    private static void WriteFixtureFiles(string root, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var content = (i % 3) switch
            {
                0 => $"export class Klass{i} {{}}\nexport interface Iface{i} {{}}\n",
                1 => $"export const K{i} = {i};\nexport function Fn{i}() {{}}\n",
                _ => $"export type T{i} = number;\nexport enum E{i} {{ A, B }}\n",
            };
            var name = $"f{i:D2}.ts";
            File.WriteAllText(Path.Combine(root, "src", name), content);
        }
    }

    private static string FindCliDll()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            foreach (var config in new[] { "Debug", "Release" })
            {
                var candidate = Path.Combine(
                    dir.FullName, "src", "SurfaceQ.Cli", "bin", config, "net8.0", "surfaceq.dll");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "surfaceq.dll not found walking up from " + AppContext.BaseDirectory);
    }
}
