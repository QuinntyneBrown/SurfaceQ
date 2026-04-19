// Acceptance Test
// Traces to: L2-011
// Description: Value exports from one source file render as a single grouped export statement.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class PublicApiRendererGroupedExportsTests
{
    [Fact]
    public void Emits_single_grouped_export_in_source_order()
    {
        var context = new ProjectContext("ng-package.json", "src/public-api.ts", "src");
        var files = new[]
        {
            new FileExports("./x", new[] { "A", "B", "C" }),
        };

        var output = new PublicApiRenderer().Render(files, context);

        Assert.Contains("export { A, B, C } from './x';", output);
        var first = output.IndexOf("export { A, B, C } from './x';", StringComparison.Ordinal);
        var second = output.IndexOf("export { A, B, C } from './x';", first + 1, StringComparison.Ordinal);
        Assert.Equal(-1, second);
    }
}
