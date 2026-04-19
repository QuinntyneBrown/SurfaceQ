// Acceptance Test
// Traces to: L2-007, L2-012
// Description: Walker returns files in deterministic ordinal-sorted order by relative path.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class SourceFileWalkerOrderingTests
{
    [Fact]
    public void Returns_files_sorted_by_relative_path_ordinal()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        var scanRoot = Path.Combine(dir, "src");
        Directory.CreateDirectory(Path.Combine(scanRoot, "B"));
        Directory.CreateDirectory(Path.Combine(scanRoot, "a"));
        try
        {
            var files = new[]
            {
                Path.Combine(scanRoot, "b.ts"),
                Path.Combine(scanRoot, "a.ts"),
                Path.Combine(scanRoot, "B", "c.ts"),
                Path.Combine(scanRoot, "a", "d.ts"),
            };
            foreach (var f in files) File.WriteAllText(f, "");

            var context = new ProjectContext(
                Path.Combine(dir, "ng-package.json"),
                Path.Combine(scanRoot, "public-api.ts"),
                scanRoot);

            var walker = new SourceFileWalker();
            var first = walker.Walk(context).ToList();
            var second = walker.Walk(context).ToList();

            Assert.Equal(first, second);

            var expected = first
                .Select(p => Path.GetRelativePath(scanRoot, p))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            var actual = first
                .Select(p => Path.GetRelativePath(scanRoot, p))
                .ToList();
            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
