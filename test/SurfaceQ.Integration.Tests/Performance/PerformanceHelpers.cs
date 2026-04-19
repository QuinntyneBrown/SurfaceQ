using System.Diagnostics;

namespace SurfaceQ.Integration.Tests.Performance;

internal static class PerformanceHelpers
{
    public static void WriteFixture(string dir, int count)
    {
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(
            Path.Combine(dir, "ng-package.json"),
            "{ \"entryFile\": \"src/public-api.ts\" }");
        for (var i = 0; i < count; i++)
        {
            var content = (i % 3) switch
            {
                0 => $"export class Klass{i} {{}}\nexport interface Iface{i} {{}}\n",
                1 => $"export const K{i} = {i};\nexport function Fn{i}() {{}}\n",
                _ => $"export type T{i} = number;\nexport enum E{i} {{ A, B }}\n",
            };
            File.WriteAllText(Path.Combine(dir, "src", $"f{i:D3}.ts"), content);
        }
    }

    public static string FindCliDll()
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

    public static ProcessStartInfo BuildGenerateStartInfo(string cliDll, string projectDir)
    {
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
        psi.ArgumentList.Add(projectDir);
        return psi;
    }
}
