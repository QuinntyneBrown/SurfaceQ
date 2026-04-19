// Acceptance Test
// Traces to: L2-006
// Description: Walker excludes *.spec.ts, *.stories.ts, and files named index.ts.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class SourceFileWalkerExclusionsTests
{
    [Fact]
    public void Excludes_spec_stories_and_index_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        var scanRoot = Path.Combine(dir, "src");
        Directory.CreateDirectory(Path.Combine(scanRoot, "sub"));
        try
        {
            var a = Path.Combine(scanRoot, "a.ts");
            var aSpec = Path.Combine(scanRoot, "a.spec.ts");
            var aStories = Path.Combine(scanRoot, "a.stories.ts");
            var subIndex = Path.Combine(scanRoot, "sub", "index.ts");
            var b = Path.Combine(scanRoot, "b.ts");
            File.WriteAllText(a, "");
            File.WriteAllText(aSpec, "");
            File.WriteAllText(aStories, "");
            File.WriteAllText(subIndex, "");
            File.WriteAllText(b, "");
            var context = new ProjectContext(
                Path.Combine(dir, "ng-package.json"),
                Path.Combine(scanRoot, "public-api.ts"),
                scanRoot);

            var files = new SourceFileWalker().Walk(context).ToList();

            Assert.Equal(2, files.Count);
            Assert.Contains(Path.GetFullPath(a), files);
            Assert.Contains(Path.GetFullPath(b), files);
            Assert.DoesNotContain(Path.GetFullPath(aSpec), files);
            Assert.DoesNotContain(Path.GetFullPath(aStories), files);
            Assert.DoesNotContain(Path.GetFullPath(subIndex), files);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
