// Acceptance Test
// Traces to: L2-011
// Description: Module specifiers use POSIX slashes, drop .ts, and are relative to entry dir.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class PublicApiRendererModuleSpecifierTests
{
    [Fact]
    public void Specifier_is_posix_relative_to_entry_without_ts()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N")));
        var context = new ProjectContext(
            Path.Combine(root, "ng-package.json"),
            Path.Combine(root, "src", "public-api.ts"),
            Path.Combine(root, "src"));
        var files = new[]
        {
            new FileExports(
                Path.Combine(root, "src", "lib", "a.ts"),
                new[] { "A" },
                Array.Empty<string>()),
        };

        var output = new PublicApiRenderer().Render(files, context);

        Assert.Contains("export { A } from './lib/a';", output);
        Assert.DoesNotContain("\\", output);
        Assert.DoesNotContain(".ts'", output);
    }
}
