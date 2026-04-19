// Acceptance Test
// Traces to: L2-011
// Description: Renderer emits the SurfaceQ header comment block for empty input.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class PublicApiRendererHeaderTests
{
    [Fact]
    public void Renders_header_block_for_empty_file_exports()
    {
        var context = new ProjectContext("ng-package.json", "src/public-api.ts", "src");

        var output = new PublicApiRenderer().Render(Array.Empty<FileExports>(), context);

        Assert.StartsWith("// ====", output);
        Assert.Contains("SurfaceQ", output);
        Assert.Contains("DO NOT EDIT", output);
    }
}
