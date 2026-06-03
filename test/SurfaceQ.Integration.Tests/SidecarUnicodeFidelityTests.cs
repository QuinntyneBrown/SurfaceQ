// Acceptance Test
// Traces to: L2-013
// Description: The Node sidecar JSON-RPC channel preserves non-ASCII characters
// end to end. The host must decode the sidecar's UTF-8 stdout as UTF-8 rather
// than the OS console's OEM code page, so a JSDoc comment's "→" (and accented
// Latin, currency, CJK, and astral/emoji code points) survives the round trip
// instead of coming back as mojibake ("ÔåÆ"). Defends the cross-platform
// byte-identical output contract for the docs/inventory commands (L2-012, L2-034).

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarUnicodeFidelityTests
{
    // Built from escapes so the test is independent of this file's on-disk
    // encoding: arrow, euro sign, CJK "日本語", and a rocket emoji (a surrogate
    // pair / 4-byte UTF-8 sequence).
    private const string Arrow = "→";
    private const string Euro = "€";
    private const string Cjk = "日本語";
    private const string Emoji = "\U0001F680";
    // The arrow's bytes (E2 86 92) decoded as CP850 — the symptom this guards against.
    private const string Cp850Mojibake = "ÔåÆ";

    [Fact]
    public void Inventory_preserves_non_ascii_characters_in_doc_comments()
    {
        var file = WriteTemp(
            $"/** Maps an input {Arrow} output. Costs {Euro}5. CJK: {Cjk}. Emoji: {Emoji}. */\n" +
            "export class Widget {}\n");
        try
        {
            var doc = Doc(file, "Widget");

            Assert.Contains(Arrow, doc);
            Assert.Contains(Euro, doc);
            Assert.Contains(Cjk, doc);
            Assert.Contains(Emoji, doc);
            Assert.DoesNotContain(Cp850Mojibake, doc);
        }
        finally
        {
            DeleteTemp(file);
        }
    }

    private static string Doc(string file, string name)
    {
        using var client = new SidecarClient(SidecarScript.ResolvePath());
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "inventory",
            @params = new { file },
        });
        var result = JsonDocument.Parse(client.Send(request)).RootElement.GetProperty("result");
        var item = result.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("name").GetString() == name);
        return item.GetProperty("doc").GetString()!;
    }

    private static string WriteTemp(string source)
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "source.ts");
        File.WriteAllText(file, source);
        return file;
    }

    private static void DeleteTemp(string file) =>
        Directory.Delete(Path.GetDirectoryName(file)!, recursive: true);
}
