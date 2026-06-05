// Acceptance Test
// Traces to: L2-036
// Description: FicdFrontmatter parses the documented YAML subset — scalars,
// quoted scalars, block lists, inline lists, comments, unknown keys, and the
// single list-of-maps key `groups` — and splits a section file's frontmatter
// from its Markdown body.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class FicdFrontmatterTests
{
    [Fact]
    public void Split_separates_fenced_frontmatter_from_body()
    {
        var text = "---\ntitle: Services\n---\nThe body.\nSecond line.\n";

        var (frontmatter, body) = FicdFrontmatter.Split(text);

        Assert.Equal("title: Services", frontmatter);
        Assert.Equal("The body.\nSecond line.\n", body);
    }

    [Fact]
    public void Split_treats_a_file_with_no_fence_as_all_body()
    {
        var (frontmatter, body) = FicdFrontmatter.Split("# Just prose\n");

        Assert.Equal("", frontmatter);
        Assert.Equal("# Just prose\n", body);
    }

    [Fact]
    public void Split_normalizes_crlf_line_endings()
    {
        var (frontmatter, body) = FicdFrontmatter.Split("---\r\nsection: 8\r\n---\r\nBody.\r\n");

        Assert.Equal("section: 8", frontmatter);
        Assert.Equal("Body.\n", body);
    }

    [Fact]
    public void Parse_reads_scalars_quoted_scalars_and_ignores_comments_and_unknown_shapes()
    {
        var block = FicdFrontmatter.Parse(
            "# a comment\n" +
            "section: 8\n" +
            "title: Core API — Services\n" +
            "package: \"@gers/mdf (Angular 17)\"\n" +
            "inventory: true\n");

        Assert.Equal(8, block.Int("section"));
        Assert.Equal("Core API — Services", block.Scalar("title"));
        Assert.Equal("@gers/mdf (Angular 17)", block.Scalar("package"));
        Assert.True(block.Flag("inventory"));
        Assert.Equal("", block.Scalar("missing"));
        Assert.Empty(block.List("missing"));
    }

    [Fact]
    public void Parse_reads_block_lists_and_inline_lists()
    {
        var block = FicdFrontmatter.Parse(
            "requirements:\n" +
            "  - A plugin **shall** inject the token.\n" +
            "  - A plugin **shall** type against the interface.\n" +
            "tags: [a, b, c]\n");

        Assert.Equal(
            new[] { "A plugin **shall** inject the token.", "A plugin **shall** type against the interface." },
            block.List("requirements"));
        Assert.Equal(new[] { "a", "b", "c" }, block.List("tags"));
    }

    [Fact]
    public void Parse_reads_groups_as_a_list_of_maps_in_order()
    {
        var block = FicdFrontmatter.Parse(
            "template: services\n" +
            "groups:\n" +
            "  - title: Outbound messaging\n" +
            "    heading: 8.3\n" +
            "    access: Action\n" +
            "    interfaces: [ICommandService, IRequestService]\n" +
            "    tokens: [COMMAND_SERVICE, REQUEST_SERVICE]\n" +
            "  - title: Streamed telemetry\n" +
            "    interfaces: [IStreamedTelemetryStore]\n" +
            "requirements:\n" +
            "  - A plugin shall release handles.\n");

        Assert.Equal("services", block.Scalar("template"));
        Assert.Equal(2, block.Groups.Count);

        var first = block.Groups[0];
        Assert.Equal("Outbound messaging", first.Scalar("title"));
        Assert.Equal("8.3", first.Scalar("heading"));
        Assert.Equal("Action", first.Scalar("access"));
        Assert.Equal(new[] { "ICommandService", "IRequestService" }, first.List("interfaces"));
        Assert.Equal(new[] { "COMMAND_SERVICE", "REQUEST_SERVICE" }, first.List("tokens"));

        var second = block.Groups[1];
        Assert.Equal("Streamed telemetry", second.Scalar("title"));
        Assert.Equal(new[] { "IStreamedTelemetryStore" }, second.List("interfaces"));

        // A top-level block list that follows the groups is still parsed.
        Assert.Equal(new[] { "A plugin shall release handles." }, block.List("requirements"));
    }

    [Fact]
    public void Parse_ignores_an_unsupported_block_sublist_inside_a_group_without_phantom_groups()
    {
        // Group list fields are inline-only; a block sub-list (the deeper `- A`/`- B`
        // lines) must not spawn phantom groups nor steal the real group's fields.
        var block = FicdFrontmatter.Parse(
            "groups:\n" +
            "  - title: First\n" +
            "    interfaces:\n" +
            "      - A\n" +
            "      - B\n" +
            "  - title: Second\n" +
            "    interfaces: [C]\n");

        Assert.Equal(2, block.Groups.Count);
        Assert.Equal("First", block.Groups[0].Scalar("title"));
        Assert.Empty(block.Groups[0].List("interfaces"));
        Assert.Equal("Second", block.Groups[1].Scalar("title"));
        Assert.Equal(new[] { "C" }, block.Groups[1].List("interfaces"));
    }

    [Fact]
    public void Parse_drops_empty_entries_from_block_lists_like_inline_lists()
    {
        var block = FicdFrontmatter.Parse("reqs:\n  - first\n  - \n  - third\n");

        Assert.Equal(new[] { "first", "third" }, block.List("reqs"));
    }

    [Fact]
    public void Split_treats_an_unclosed_fence_as_all_frontmatter()
    {
        var (frontmatter, body) = FicdFrontmatter.Split("---\ntitle: X\nmore: y");

        Assert.Equal("title: X\nmore: y", frontmatter);
        Assert.Equal("", body);
    }
}
