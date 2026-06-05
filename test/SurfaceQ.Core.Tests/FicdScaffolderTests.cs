// Acceptance Test
// Traces to: L2-039
// Description: FicdScaffolder produces the starter ficd/ files — a manifest plus
// placeholder section files (mirroring the FICD output tree) with commented-out
// `groups` examples and `<!-- add … here -->` guidance — deterministically and
// with LF endings.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class FicdScaffolderTests
{
    [Fact]
    public void Seeds_the_manifest_and_placeholder_section_tree()
    {
        var files = new FicdScaffolder().Scaffold("api");
        var paths = files.Select(f => f.RelativePath).ToArray();

        Assert.Equal(new[]
        {
            "ficd.yml",
            "introduction.md",
            "detailed-interface-definition/overview.md",
            "detailed-interface-definition/api-interface/core-api/services.md",
            "detailed-interface-definition/api-interface/core-api/functions.md",
            "detailed-interface-definition/api-interface/data-object-definitions.md",
        }, paths);

        var manifest = Content(files, "ficd.yml");
        Assert.Contains("interfaceSlug: api-interface", manifest);
        Assert.Contains("package: \"api\"", manifest);
        Assert.Contains("documentTitle: TODO", manifest);
        Assert.Contains("- detailed-interface-definition/api-interface/core-api/services.md", manifest);

        var services = Content(files, "detailed-interface-definition/api-interface/core-api/services.md");
        Assert.Contains("template: services", services);
        Assert.Contains("# groups:", services);
        Assert.Contains("#     interfaces: [IMyService]", services);
        Assert.Contains("<!-- add services here", services);
    }

    [Fact]
    public void Slugifies_a_scoped_or_mixed_case_library_name()
    {
        var files = new FicdScaffolder().Scaffold("@acme/Auth");

        Assert.Contains(files, f =>
            f.RelativePath == "detailed-interface-definition/acme-auth-interface/core-api/services.md");
    }

    [Fact]
    public void Output_is_deterministic_and_lf_terminated()
    {
        var first = new FicdScaffolder().Scaffold("lib");
        var second = new FicdScaffolder().Scaffold("lib");

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].RelativePath, second[i].RelativePath);
            Assert.Equal(first[i].Content, second[i].Content);
            Assert.DoesNotContain('\r', second[i].Content);
            Assert.EndsWith("\n", second[i].Content);
        }
    }

    private static string Content(IReadOnlyList<FicdOutputFile> files, string path) =>
        files.First(f => f.RelativePath == path).Content;
}
