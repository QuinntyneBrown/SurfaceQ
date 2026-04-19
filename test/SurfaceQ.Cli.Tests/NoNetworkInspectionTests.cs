// Acceptance Test
// Traces to: L2-017
// Description: Inspection — no SurfaceQ assembly references networking types and sidecar.js has no HTTP/net imports.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using SurfaceQ.Cli;
using SurfaceQ.Core;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class NoNetworkInspectionTests
{
    private static readonly HashSet<string> ForbiddenTypeRefs = new(StringComparer.Ordinal)
    {
        "System.Net.Http.HttpClient",
        "System.Net.Http.HttpRequestMessage",
        "System.Net.Http.HttpResponseMessage",
        "System.Net.WebClient",
        "System.Net.WebRequest",
        "System.Net.HttpWebRequest",
        "System.Net.Sockets.TcpClient",
        "System.Net.Sockets.UdpClient",
        "System.Net.Sockets.Socket",
    };

    [Fact]
    public void SurfaceQ_assemblies_do_not_reference_networking_types()
    {
        var paths = new[]
        {
            typeof(Program).Assembly.Location,
            typeof(ProjectContext).Assembly.Location,
            typeof(SidecarClient).Assembly.Location,
        };
        foreach (var path in paths)
        {
            AssertAssemblyHasNoNetworkingTypeRefs(path);
        }
    }

    [Fact]
    public void Sidecar_js_does_not_import_http_or_net_modules()
    {
        var scriptPath = ResolveSidecarScript();
        var content = File.ReadAllText(scriptPath);
        string[] forbidden =
        {
            "require('http')", "require(\"http\")",
            "require('https')", "require(\"https\")",
            "require('net')", "require(\"net\")",
            "require('tls')", "require(\"tls\")",
            "require('dgram')", "require(\"dgram\")",
            "require('node-fetch')", "require(\"node-fetch\")",
            "fetch(", "XMLHttpRequest",
        };
        foreach (var needle in forbidden)
        {
            Assert.DoesNotContain(needle, content);
        }
    }

    private static void AssertAssemblyHasNoNetworkingTypeRefs(string path)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var mdReader = peReader.GetMetadataReader();
        foreach (var handle in mdReader.TypeReferences)
        {
            var tref = mdReader.GetTypeReference(handle);
            var ns = mdReader.GetString(tref.Namespace);
            var name = mdReader.GetString(tref.Name);
            var full = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            Assert.False(
                ForbiddenTypeRefs.Contains(full),
                $"{path} references forbidden networking type '{full}'");
        }
    }

    private static string ResolveSidecarScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SurfaceQ.Sidecar.Node", "sidecar.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("sidecar.js not found from " + AppContext.BaseDirectory);
    }
}
