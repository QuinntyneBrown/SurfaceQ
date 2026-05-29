// Acceptance Test
// Traces to: L2-023
// Description: WorkspaceLocator finds every ng-package.json under a workspace,
// excludes node_modules and dist, and returns results in deterministic order.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class WorkspaceLocatorTests
{
    [Fact]
    public void Finds_all_library_manifests_excluding_node_modules_and_dist()
    {
        var ws = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ws, "libs", "auth"));
        Directory.CreateDirectory(Path.Combine(ws, "libs", "data"));
        Directory.CreateDirectory(Path.Combine(ws, "node_modules", "pkg"));
        Directory.CreateDirectory(Path.Combine(ws, "dist", "auth"));
        try
        {
            var auth = Path.Combine(ws, "libs", "auth", "ng-package.json");
            var data = Path.Combine(ws, "libs", "data", "ng-package.json");
            File.WriteAllText(auth, "{}");
            File.WriteAllText(data, "{}");
            File.WriteAllText(Path.Combine(ws, "node_modules", "pkg", "ng-package.json"), "{}");
            File.WriteAllText(Path.Combine(ws, "dist", "auth", "ng-package.json"), "{}");

            var found = new WorkspaceLocator().FindLibraries(ws);

            Assert.Equal(2, found.Count);
            Assert.Equal(Path.GetFullPath(auth), found[0]);
            Assert.Equal(Path.GetFullPath(data), found[1]);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public void Missing_workspace_returns_empty()
    {
        var missing = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Assert.Empty(new WorkspaceLocator().FindLibraries(missing));
    }
}
