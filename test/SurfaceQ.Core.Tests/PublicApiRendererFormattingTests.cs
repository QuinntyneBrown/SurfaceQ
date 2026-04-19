// Acceptance Test
// Traces to: L2-011
// Description: Output uses LF line endings, no tabs, and ends with exactly one newline.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class PublicApiRendererFormattingTests
{
    [Fact]
    public void Output_ends_with_single_newline_and_has_no_tabs()
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

        Assert.EndsWith("\n", output);
        Assert.False(output.EndsWith("\n\n"), "output must not end with two newlines");
        Assert.DoesNotContain("\t", output);
        Assert.DoesNotContain("\r", output);
    }

    [Fact]
    public void Empty_output_still_ends_with_single_newline()
    {
        var context = new ProjectContext("ng-package.json", "src/public-api.ts", "src");

        var output = new PublicApiRenderer().Render(Array.Empty<FileExports>(), context);

        Assert.EndsWith("\n", output);
        Assert.False(output.EndsWith("\n\n"), "empty output must not end with two newlines");
        Assert.DoesNotContain("\t", output);
        Assert.DoesNotContain("\r", output);
    }
}
