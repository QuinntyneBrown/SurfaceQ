// Acceptance Test
// Traces to: L2-006
// Description: Walker excludes the entryFile itself and any file inside node_modules.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class SourceFileWalkerEntryAndNodeModulesTests
{
    [Fact]
    public void Excludes_entryFile_and_node_modules_contents()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        var scanRoot = Path.Combine(dir, "src");
        Directory.CreateDirectory(Path.Combine(scanRoot, "node_modules", "foo"));
        try
        {
            var entry = Path.Combine(scanRoot, "public-api.ts");
            var a = Path.Combine(scanRoot, "a.ts");
            var nm = Path.Combine(scanRoot, "node_modules", "foo", "bar.ts");
            File.WriteAllText(entry, "");
            File.WriteAllText(a, "");
            File.WriteAllText(nm, "");
            var context = new ProjectContext(
                Path.Combine(dir, "ng-package.json"),
                entry,
                scanRoot);

            var files = new SourceFileWalker().Walk(context).ToList();

            Assert.Single(files);
            Assert.Contains(Path.GetFullPath(a), files);
            Assert.DoesNotContain(Path.GetFullPath(entry), files);
            Assert.DoesNotContain(Path.GetFullPath(nm), files);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
