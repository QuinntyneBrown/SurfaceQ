// Acceptance Test
// Traces to: L2-036
// Description: FicdMetadataReader reads the ficd/ folder into a FicdProject —
// parsing ficd.yml (or ficd.yaml) identity, deriving a default section title from
// the file stem, ordering sections by Ordinal path, trimming bodies, excluding an
// authored README.md, and dropping content-less groups — and degrades to empty
// identity when the manifest is absent.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class FicdMetadataReaderTests
{
    [Fact]
    public void Reads_manifest_identity_and_default_section_title_from_stem()
    {
        var dir = NewFicdDir();
        try
        {
            Write(dir, "ficd.yml", "documentTitle: Auth FICD\nversion: 1.0 (Draft)\n");
            // No `title:` -> derived from the hyphenated file stem.
            Write(dir, "detailed/discovery-and-registration.md", "---\nsection: 4\n---\nBody.\n");

            var project = new FicdMetadataReader().Read(dir, "@acme/auth");

            Assert.Equal("Auth FICD", project.Manifest.DocumentTitle);
            Assert.Equal("1.0 (Draft)", project.Manifest.Version);
            var section = Assert.Single(project.Sections);
            Assert.Equal("detailed/discovery-and-registration.md", section.RelativePath);
            Assert.Equal("Discovery And Registration", section.Title);
            Assert.Equal("narrative", section.Template);
            Assert.Equal("Body.", section.Body);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Falls_back_to_empty_identity_when_manifest_is_absent()
    {
        var dir = NewFicdDir();
        try
        {
            Write(dir, "introduction.md", "---\ntitle: Introduction\n---\nProse.\n");

            var project = new FicdMetadataReader().Read(dir, "lib");

            Assert.Equal("", project.Manifest.DocumentTitle);
            Assert.Empty(project.Manifest.Documents);
            Assert.Single(project.Sections);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Reads_ficd_yaml_when_ficd_yml_is_absent()
    {
        var dir = NewFicdDir();
        try
        {
            Write(dir, "ficd.yaml", "documentTitle: YAML FICD\n");
            Write(dir, "overview.md", "---\ntitle: Overview\n---\n");

            var project = new FicdMetadataReader().Read(dir, "lib");

            Assert.Equal("YAML FICD", project.Manifest.DocumentTitle);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Orders_sections_by_ordinal_path_and_trims_bodies()
    {
        var dir = NewFicdDir();
        try
        {
            Write(dir, "b.md", "---\ntitle: B\n---\n");
            Write(dir, "a.md", "---\ntitle: A\n---\n   spaced body   \n\n");

            var project = new FicdMetadataReader().Read(dir, "lib");

            Assert.Equal(new[] { "a.md", "b.md" }, project.Sections.Select(s => s.RelativePath));
            Assert.Equal("spaced body", project.Sections[0].Body);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Excludes_an_authored_readme_from_the_sections()
    {
        var dir = NewFicdDir();
        try
        {
            Write(dir, "README.md", "---\ntitle: Hand-written\n---\nShould not become a section.\n");
            Write(dir, "introduction.md", "---\ntitle: Introduction\n---\n");

            var project = new FicdMetadataReader().Read(dir, "lib");

            var section = Assert.Single(project.Sections);
            Assert.Equal("introduction.md", section.RelativePath);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Drops_a_group_with_no_content()
    {
        var dir = NewFicdDir();
        try
        {
            // The second group is a bare "- " (no fields) and must be dropped so it
            // never renders a phantom heading.
            Write(dir, "services.md",
                "---\n" +
                "template: services\n" +
                "groups:\n" +
                "  - title: Real\n" +
                "    interfaces: [A]\n" +
                "  - \n" +
                "---\n");

            var project = new FicdMetadataReader().Read(dir, "lib");

            var section = Assert.Single(project.Sections);
            var group = Assert.Single(section.Groups);
            Assert.Equal("Real", group.Title);
            Assert.Equal(new[] { "A" }, group.Interfaces);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string NewFicdDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-ficd-" + Guid.NewGuid().ToString("N"), "ficd");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Write(string ficdDir, string relative, string content)
    {
        var path = Path.Combine(ficdDir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
