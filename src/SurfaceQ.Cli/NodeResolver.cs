using System.Runtime.InteropServices;

namespace SurfaceQ.Cli;

internal static class NodeResolver
{
    public static string ResolveNodePath() => ResolveNodePath(AppContext.BaseDirectory);

    internal static string ResolveNodePath(string baseDir)
    {
        var rid = GetRid();
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var exe = isWindows ? "node.exe" : "node";
        var candidate = Path.Combine(baseDir, "content", "node", rid, exe);
        if (!File.Exists(candidate))
        {
            throw new FileNotFoundException(
                $"bundled node not found for RID '{rid}' at '{candidate}'");
        }
        return candidate;
    }

    private static string GetRid()
    {
        var os = GetOsKey();
        var arch = GetArchKey();
        return $"{os}-{arch}";
    }

    private static string GetOsKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "osx";
        }
        throw new PlatformNotSupportedException("unsupported OS");
    }

    private static string GetArchKey()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException("SurfaceQ supports x64 and arm64 only"),
        };
    }
}
