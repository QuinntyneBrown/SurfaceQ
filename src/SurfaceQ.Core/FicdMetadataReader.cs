namespace SurfaceQ.Core;

// Reads a library's `ficd/` authoring folder into a FicdProject: the `ficd.yml`
// manifest (global identity + document order) plus one FicdSection per authored
// `*.md` file. Section relative paths are POSIX and preserved verbatim so the
// output tree mirrors the input tree. Sections are returned in Ordinal path
// order for deterministic rendering. See docs/specs/ficd-schema.md.

public sealed class FicdMetadataReader
{
    public FicdProject Read(string ficdDir, string libraryName)
    {
        var root = Path.GetFullPath(ficdDir);
        return new FicdProject(libraryName, ReadManifest(root), ReadSections(root));
    }

    private static FicdManifest ReadManifest(string ficdDir)
    {
        var path = Path.Combine(ficdDir, "ficd.yml");
        if (!File.Exists(path))
        {
            path = Path.Combine(ficdDir, "ficd.yaml");
        }
        if (!File.Exists(path))
        {
            return EmptyManifest();
        }
        var block = FicdFrontmatter.Parse(File.ReadAllText(path));
        return new FicdManifest(
            block.Scalar("documentTitle"),
            block.Scalar("documentNumber"),
            block.Scalar("version"),
            block.Scalar("date"),
            block.Scalar("interface"),
            block.Scalar("package"),
            block.Scalar("status"),
            block.Scalar("changeAuthority"),
            block.List("documents"));
    }

    private static FicdManifest EmptyManifest() =>
        new("", "", "", "", "", "", "", "", Array.Empty<string>());

    private static IReadOnlyList<FicdSection> ReadSections(string ficdDir)
    {
        if (!Directory.Exists(ficdDir))
        {
            return Array.Empty<FicdSection>();
        }
        var sections = new List<FicdSection>();
        foreach (var file in Directory.EnumerateFiles(ficdDir, "*.md", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(ficdDir, Path.GetFullPath(file)).Replace('\\', '/');
            // README.md is always generated from the manifest; an authored one at
            // the same relative path would otherwise clobber (or be clobbered by)
            // the generated index, so it is never treated as a section.
            if (string.Equals(relative, "README.md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            sections.Add(ReadSection(file, relative));
        }
        return sections
            .OrderBy(s => s.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    private static FicdSection ReadSection(string file, string relativePath)
    {
        var (frontmatter, body) = FicdFrontmatter.Split(File.ReadAllText(file));
        var block = FicdFrontmatter.Parse(frontmatter);
        var template = block.Scalar("template");
        var title = block.Scalar("title");
        return new FicdSection(
            relativePath,
            template.Length > 0 ? template : "narrative",
            block.Int("section"),
            title.Length > 0 ? title : DefaultTitle(relativePath),
            block.Scalar("scope"),
            block.Scalar("summary"),
            block.Flag("inventory"),
            ReadGroups(block),
            block.List("requirements"),
            block.List("crossReferences"),
            body.Trim());
    }

    private static IReadOnlyList<FicdGroup> ReadGroups(FrontmatterBlock block)
    {
        var groups = new List<FicdGroup>();
        foreach (var g in block.Groups)
        {
            var group = new FicdGroup(
                g.Scalar("title"),
                g.Scalar("heading"),
                g.Scalar("intro"),
                g.Scalar("access"),
                g.List("interfaces"),
                g.List("tokens"),
                g.List("classes"),
                g.List("functions"),
                g.List("types"),
                g.List("dataObjects"));
            // Skip a group that carries nothing (e.g. a stray `- ` line or an
            // unsupported block sub-list) so it never renders a phantom heading.
            if (!IsEmptyGroup(group))
            {
                groups.Add(group);
            }
        }
        return groups;
    }

    private static bool IsEmptyGroup(FicdGroup g) =>
        g.Title.Length == 0 && g.Heading.Length == 0 && g.Intro.Length == 0 && g.Access.Length == 0
        && g.Interfaces.Count == 0 && g.Tokens.Count == 0 && g.Classes.Count == 0
        && g.Functions.Count == 0 && g.Types.Count == 0 && g.DataObjects.Count == 0;

    // Title for a section file that omits `title`: the file stem with hyphens and
    // underscores turned into spaced, capitalized words ("core-api/services.md"
    // -> "Services").
    private static string DefaultTitle(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var words = stem.Split('-', '_')
            .Where(w => w.Length > 0)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);
        return string.Join(" ", words);
    }
}
