// Acceptance Test
// Traces to: L2-004
// Description: Locator finds ng-package.json in the start directory.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class ProjectLocatorTests
{
    [Fact]
    public void Finds_manifest_in_start_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            File.WriteAllText(manifest, "{}");

            var result = new ProjectLocator().Locate(dir);

            Assert.Equal(manifest, result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
