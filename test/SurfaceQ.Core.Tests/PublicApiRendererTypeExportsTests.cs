// Acceptance Test
// Traces to: L2-011
// Description: Renderer emits value line then type-only line for files with both.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class PublicApiRendererTypeExportsTests
{
    [Fact]
    public void Emits_value_line_then_type_line_for_same_file()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N")));
        var context = new ProjectContext(
            Path.Combine(root, "ng-package.json"),
            Path.Combine(root, "src", "public-api.ts"),
            Path.Combine(root, "src"));
        var files = new[]
        {
            new FileExports(
                Path.Combine(root, "src", "f.ts"),
                new[] { "X" },
                new[] { "Y" }),
        };

        var output = new PublicApiRenderer().Render(files, context);

        var valueIdx = output.IndexOf("export { X } from './f';", StringComparison.Ordinal);
        var typeIdx = output.IndexOf("export type { Y } from './f';", StringComparison.Ordinal);
        Assert.True(valueIdx >= 0, "value export line missing");
        Assert.True(typeIdx >= 0, "type export line missing");
        Assert.True(valueIdx < typeIdx, "value line must precede type line");
    }
}
