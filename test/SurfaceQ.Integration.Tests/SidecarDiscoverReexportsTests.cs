// Acceptance Test
// Traces to: L2-008
// Description: Named re-exports preserve name and type-only flag.

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarDiscoverReexportsTests
{
    [Fact]
    public void Preserves_named_reexports_with_type_only_flag()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "a.ts");
            File.WriteAllText(
                file,
                "export { Foo } from './b';\n" +
                "export type { Bar } from './b';\n");

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
            var result = doc.RootElement.GetProperty("result").GetProperty("exports");
            var entries = new Dictionary<string, bool>();
            foreach (var e in result.EnumerateArray())
            {
                entries[e.GetProperty("name").GetString()!] = e.GetProperty("isType").GetBoolean();
            }

            Assert.True(entries.ContainsKey("Foo"), "Foo should be reported");
            Assert.False(entries["Foo"], "Foo should be a value re-export");
            Assert.True(entries.ContainsKey("Bar"), "Bar should be reported");
            Assert.True(entries["Bar"], "Bar should be a type-only re-export");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
