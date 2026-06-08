// Acceptance Test
// Traces to: L2-011
// Description: Renderer emits no generated/regenerate header comments.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class PublicApiRendererHeaderTests
{
    [Fact]
    public void Renders_no_header_comments_for_empty_file_exports()
    {
        var context = new ProjectContext("ng-package.json", "src/public-api.ts", "src");

        var output = new PublicApiRenderer().Render(Array.Empty<FileExports>(), context);

        Assert.Equal("", output);
    }
}
