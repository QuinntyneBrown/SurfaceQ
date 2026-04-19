// Acceptance Test
// Traces to: L2-008
// Description: Sidecar discover returns exported class declarations as { name, kind, isType }.

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarDiscoverClassTests
{
    [Fact]
    public void Discovers_exported_class()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "foo.ts");
            File.WriteAllText(file, "export class Foo {}\n");

            using var client = new SidecarClient(SidecarScript.ResolvePath());
            var request = JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "discover",
                @params = new { file },
            });
            var responseJson = client.Send(request);

            using var doc = JsonDocument.Parse(responseJson);
            var result = doc.RootElement.GetProperty("result");
            Assert.Equal(1, result.GetArrayLength());
            var entry = result[0];
            Assert.Equal("Foo", entry.GetProperty("name").GetString());
            Assert.Equal("class", entry.GetProperty("kind").GetString());
            Assert.False(entry.GetProperty("isType").GetBoolean());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
