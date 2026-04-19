// Acceptance Test
// Traces to: L2-014
// Description: NodeResolver returns an existing bundled-node path for the current RID.

using SurfaceQ.Cli;
using Xunit;

namespace SurfaceQ.Cli.Tests;

public class NodeResolverTests
{
    [Fact]
    public void Resolves_existing_path_for_current_rid()
    {
        var path = NodeResolver.ResolveNodePath();
        Assert.True(File.Exists(path), $"expected bundled node at '{path}'");
    }
}
