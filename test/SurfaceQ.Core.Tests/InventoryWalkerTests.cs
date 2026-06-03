// Acceptance Test
// Traces to: L2-033
// Description: InventoryWalker returns production .ts sources only — excluding
// tests, stories, e2e specs, ambient .d.ts typings, and anything under
// node_modules or dist — in deterministic Ordinal order.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class InventoryWalkerTests
{
    [Fact]
    public void Walk_keeps_production_sources_and_excludes_tests_and_typings()
    {
        var root = NewDir();
        try
        {
            Write(root, "widget.ts", "export class Widget {}");
            Write(root, "widget.spec.ts", "export class WidgetSpec {}");
            Write(root, "widget.stories.ts", "export const story = {};");
            Write(root, "widget.e2e-spec.ts", "export class E2e {}");
            Write(root, "smoke.e2e.ts", "export class Smoke {}");
            Write(root, "types.d.ts", "declare const x: number;");
            Write(Path.Combine(root, "node_modules", "pkg"), "dep.ts", "export class Dep {}");
            Write(Path.Combine(root, "dist"), "built.ts", "export class Built {}");

            var files = new InventoryWalker().Walk(root).Select(Path.GetFileName).ToList();

            Assert.Equal(new[] { "widget.ts" }, files);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Walk_returns_sources_in_ordinal_relative_path_order()
    {
        var root = NewDir();
        try
        {
            Write(Path.Combine(root, "b"), "two.ts", "export const b = 1;");
            Write(Path.Combine(root, "a"), "one.ts", "export const a = 1;");
            Write(root, "top.ts", "export const t = 1;");

            var relative = new InventoryWalker().Walk(root)
                .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
                .ToList();

            Assert.Equal(new[] { "a/one.ts", "b/two.ts", "top.ts" }, relative);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Walk_returns_empty_when_source_root_is_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Assert.Empty(new InventoryWalker().Walk(missing));
    }

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Write(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), content);
    }
}
