// Acceptance Test
// Traces to: L2-013
// Description: Sidecar spawns via node, echoes request id, and replies with result "pong".

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarPingTests
{
    [Fact]
    public void Ping_returns_pong_and_echoes_id()
    {
        var scriptPath = ResolveSidecarScript();
        using var client = new SidecarClient(scriptPath);

        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 42,
            method = "ping",
        });
        var responseJson = client.Send(request);

        using var doc = JsonDocument.Parse(responseJson);
        Assert.Equal(42, doc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("pong", doc.RootElement.GetProperty("result").GetString());
    }

    private static string ResolveSidecarScript()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(SidecarPingTests).Assembly.Location)!);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SurfaceQ.Sidecar.Node", "sidecar.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("sidecar.js not found by walking upward from test assembly location");
    }
}
