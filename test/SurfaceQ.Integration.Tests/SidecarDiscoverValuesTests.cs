// Acceptance Test
// Traces to: L2-008
// Description: Sidecar discovers exported const, function, and InjectionToken as value exports.

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarDiscoverValuesTests
{
    [Fact]
    public void Discovers_const_function_and_injectionToken()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "values.ts");
            File.WriteAllText(
                file,
                "import { InjectionToken } from '@angular/core';\n" +
                "export const X = 1;\n" +
                "export function Y() {}\n" +
                "export const TOKEN = new InjectionToken<string>('TOKEN');\n");

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
            var entries = new Dictionary<string, (string Kind, bool IsType)>();
            foreach (var e in result.EnumerateArray())
            {
                entries[e.GetProperty("name").GetString()!] =
                    (e.GetProperty("kind").GetString()!, e.GetProperty("isType").GetBoolean());
            }

            Assert.Equal(("const", false), entries["X"]);
            Assert.Equal(("function", false), entries["Y"]);
            Assert.Equal(("const", false), entries["TOKEN"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
