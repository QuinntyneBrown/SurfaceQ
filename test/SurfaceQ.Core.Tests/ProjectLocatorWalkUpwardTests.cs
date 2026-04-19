// Acceptance Test
// Traces to: L2-004
// Description: Locator walks upward until it finds ng-package.json.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class ProjectLocatorWalkUpwardTests
{
    [Fact]
    public void Walks_up_from_nested_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "a", "b", "c");
        Directory.CreateDirectory(nested);
        try
        {
            var manifest = Path.Combine(root, "ng-package.json");
            File.WriteAllText(manifest, "{}");

            var result = new ProjectLocator().Locate(nested);

            Assert.Equal(manifest, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
