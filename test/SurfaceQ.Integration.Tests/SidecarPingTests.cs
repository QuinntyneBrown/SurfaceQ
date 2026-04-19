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
        using var client = new SidecarClient(SidecarScript.ResolvePath());

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
}
