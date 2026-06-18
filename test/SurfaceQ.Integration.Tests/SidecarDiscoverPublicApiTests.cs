// Acceptance Test
// Traces to: L2-045
// Description: Sidecar discover reports a publicApi boolean per export — true for
// declarations whose JSDoc carries @publicApi (tag must start a JSDoc line),
// resolved at the declaration site for named re-exports (renames, chained
// barrels, export-star index barrels, and import-then-export included).

using System.Text.Json;
using SurfaceQ.Sidecar;
using Xunit;

namespace SurfaceQ.Integration.Tests;

public class SidecarDiscoverPublicApiTests
{
    [Fact]
    public void Reports_publicApi_for_declarations_and_resolved_reexports()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "x.ts"),
                "/** @publicApi */\n" +
                "export interface X { id: string; }\n" +
                "\n" +
                "export interface Z { id: string; }\n" +
                "\n" +
                "/** Mentions the @publicApi tag in prose only. */\n" +
                "export interface P { id: string; }\n");
            File.WriteAllText(
                Path.Combine(dir, "barrel.ts"),
                "export type { X as PublicX, Z as PublicZ, P as PublicP } from './x';\n");

            using var client = new SidecarClient(SidecarScript.ResolvePath());
            var declared = Discover(client, 1, Path.Combine(dir, "x.ts"));
            var reexported = Discover(client, 2, Path.Combine(dir, "barrel.ts"));

            Assert.True(declared["X"]);
            Assert.False(declared["Z"]);
            Assert.False(declared["P"]);
            Assert.True(reexported["PublicX"]);
            Assert.False(reexported["PublicZ"]);
            Assert.False(reexported["PublicP"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Resolves_tags_through_chained_barrels_and_imports()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "shared"));
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "a.ts"),
                "/** @publicApi */\n" +
                "export class A {}\n" +
                "\n" +
                "export class NotTagged {}\n");
            File.WriteAllText(
                Path.Combine(dir, "mid.ts"),
                "export { A, NotTagged } from './a';\n");
            File.WriteAllText(
                Path.Combine(dir, "chain.ts"),
                "export { A as ChainA, NotTagged as ChainNot } from './mid';\n");
            File.WriteAllText(
                Path.Combine(dir, "shared", "index.ts"),
                "export * from '../a';\n");
            File.WriteAllText(
                Path.Combine(dir, "wild.ts"),
                "export { A as WildA } from './shared';\n");
            File.WriteAllText(
                Path.Combine(dir, "imp.ts"),
                "import { A } from './a';\n" +
                "export { A };\n");

            using var client = new SidecarClient(SidecarScript.ResolvePath());
            var chained = Discover(client, 1, Path.Combine(dir, "chain.ts"));
            var wildcard = Discover(client, 2, Path.Combine(dir, "wild.ts"));
            var imported = Discover(client, 3, Path.Combine(dir, "imp.ts"));

            Assert.True(chained["ChainA"]);
            Assert.False(chained["ChainNot"]);
            Assert.True(wildcard["WildA"]);
            Assert.True(imported["A"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static Dictionary<string, bool> Discover(SidecarClient client, int id, string file)
    {
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            method = "discover",
            @params = new { file },
        });
        var responseJson = client.Send(request);
        using var doc = JsonDocument.Parse(responseJson);
        var entries = new Dictionary<string, bool>();
        foreach (var e in doc.RootElement.GetProperty("result").GetProperty("exports").EnumerateArray())
        {
            entries[e.GetProperty("name").GetString()!] = e.GetProperty("publicApi").GetBoolean();
        }
        return entries;
    }
}
