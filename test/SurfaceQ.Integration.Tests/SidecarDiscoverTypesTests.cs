// Acceptance Test
// Traces to: L2-008
// Description: Sidecar discovers interfaces, type aliases, enums, and const enums with correct isType.

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarDiscoverTypesTests
{
    [Fact]
    public void Discovers_interface_typeAlias_enum_and_constEnum()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "types.ts");
            File.WriteAllText(
                file,
                "export interface Bar {}\n" +
                "export type Baz = number;\n" +
                "export enum E { A }\n" +
                "export const enum F { B }\n");

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
            var entries = new Dictionary<string, (string Kind, bool IsType)>();
            foreach (var e in result.EnumerateArray())
            {
                entries[e.GetProperty("name").GetString()!] =
                    (e.GetProperty("kind").GetString()!, e.GetProperty("isType").GetBoolean());
            }

            Assert.Equal(("interface", true), entries["Bar"]);
            Assert.Equal(("type", true), entries["Baz"]);
            Assert.Equal(("enum", false), entries["E"]);
            Assert.Equal(("enum", false), entries["F"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
