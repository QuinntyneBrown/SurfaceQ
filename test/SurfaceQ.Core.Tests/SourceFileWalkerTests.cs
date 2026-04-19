// Acceptance Test
// Traces to: L2-006
// Description: SourceFileWalker returns every .ts file under ScanRoot recursively.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class SourceFileWalkerTests
{
    [Fact]
    public void Walks_ts_files_recursively_and_ignores_non_ts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        var scanRoot = Path.Combine(dir, "src");
        Directory.CreateDirectory(Path.Combine(scanRoot, "b"));
        try
        {
            var a = Path.Combine(scanRoot, "a.ts");
            var c = Path.Combine(scanRoot, "b", "c.ts");
            var d = Path.Combine(scanRoot, "d.js");
            File.WriteAllText(a, "");
            File.WriteAllText(c, "");
            File.WriteAllText(d, "");
            var context = new ProjectContext(
                Path.Combine(dir, "ng-package.json"),
                Path.Combine(scanRoot, "public-api.ts"),
                scanRoot);

            var files = new SourceFileWalker().Walk(context).ToList();

            Assert.Equal(2, files.Count);
            Assert.Contains(Path.GetFullPath(a), files);
            Assert.Contains(Path.GetFullPath(c), files);
            Assert.DoesNotContain(Path.GetFullPath(d), files);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
