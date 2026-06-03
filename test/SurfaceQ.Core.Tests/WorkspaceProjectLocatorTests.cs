// Acceptance Test
// Traces to: L2-032
// Description: WorkspaceProjectLocator discovers applications (angular.json) and
// libraries (ng-package.json), excludes node_modules/dist and *-e2e projects,
// and falls back to treating the target itself as a single application.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class WorkspaceProjectLocatorTests
{
    [Fact]
    public void Finds_application_and_library_from_angular_json()
    {
        var ws = NewDir();
        try
        {
            Dir(ws, "apps", "web", "src");
            Dir(ws, "libs", "data", "src");
            // "data" omits sourceRoot to exercise the <projectRoot>/src default.
            Write(ws, "angular.json",
                "{ \"projects\": {" +
                "  \"web\": { \"projectType\": \"application\", \"root\": \"apps/web\", \"sourceRoot\": \"apps/web/src\" }," +
                "  \"data\": { \"projectType\": \"library\", \"root\": \"libs/data\" }" +
                "} }");

            var projects = new WorkspaceProjectLocator().Find(ws);

            var web = projects.Single(p => p.Name == "web");
            Assert.Equal("application", web.Type);
            Assert.Equal(Path.Combine(ws, "apps", "web"), web.ProjectDir);
            Assert.Equal(Path.Combine(ws, "apps", "web", "src"), web.SourceRoot);

            var data = projects.Single(p => p.Name == "data");
            Assert.Equal("library", data.Type);
            Assert.Equal(Path.Combine(ws, "libs", "data", "src"), data.SourceRoot);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public void Finds_library_from_ng_package_and_skips_node_modules_and_dist()
    {
        var ws = NewDir();
        try
        {
            Dir(ws, "libs", "auth", "src");
            Write(ws, Path.Combine("libs", "auth", "ng-package.json"), "{ \"entryFile\": \"src/public-api.ts\" }");
            Write(ws, Path.Combine("libs", "auth", "package.json"), "{ \"name\": \"@acme/auth\" }");
            // Decoys that must be ignored.
            Dir(ws, "node_modules", "@angular", "core", "src");
            Write(ws, Path.Combine("node_modules", "@angular", "core", "ng-package.json"), "{ }");
            Dir(ws, "dist", "auth", "src");
            Write(ws, Path.Combine("dist", "auth", "ng-package.json"), "{ }");

            var projects = new WorkspaceProjectLocator().Find(ws);

            var auth = Assert.Single(projects);
            Assert.Equal("@acme/auth", auth.Name);
            Assert.Equal("library", auth.Type);
            Assert.Equal(Path.Combine(ws, "libs", "auth", "src"), auth.SourceRoot);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public void Skips_e2e_projects()
    {
        var ws = NewDir();
        try
        {
            Dir(ws, "apps", "web", "src");
            Dir(ws, "apps", "web-e2e", "src");
            Write(ws, "angular.json",
                "{ \"projects\": {" +
                "  \"web\": { \"projectType\": \"application\", \"root\": \"apps/web\" }," +
                "  \"web-e2e\": { \"projectType\": \"application\", \"root\": \"apps/web-e2e\" }" +
                "} }");

            var projects = new WorkspaceProjectLocator().Find(ws);

            Assert.Single(projects);
            Assert.Equal("web", projects[0].Name);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public void Finds_nx_project_from_project_json_honoring_source_root()
    {
        var ws = NewDir();
        try
        {
            // Nx sourceRoot is workspace-root-relative and can differ from /src.
            Dir(ws, "apps", "api", "source");
            Write(ws, Path.Combine("apps", "api", "project.json"),
                "{ \"name\": \"api\", \"projectType\": \"application\", \"sourceRoot\": \"apps/api/source\" }");

            var projects = new WorkspaceProjectLocator().Find(ws);

            var api = Assert.Single(projects);
            Assert.Equal("api", api.Name);
            Assert.Equal("application", api.Type);
            Assert.Equal(Path.Combine(ws, "apps", "api", "source"), api.SourceRoot);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public void Finds_project_from_string_valued_angular_json_entry()
    {
        var ws = NewDir();
        try
        {
            Dir(ws, "apps", "shell", "src");
            Write(ws, Path.Combine("apps", "shell", "project.json"),
                "{ \"name\": \"shell\", \"projectType\": \"application\" }");
            Write(ws, "angular.json", "{ \"projects\": { \"shell\": \"apps/shell\" } }");

            var projects = new WorkspaceProjectLocator().Find(ws);

            var shell = Assert.Single(projects);
            Assert.Equal("shell", shell.Name);
            Assert.Equal("application", shell.Type);
            Assert.Equal(Path.Combine(ws, "apps", "shell", "src"), shell.SourceRoot);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    [Fact]
    public void Falls_back_to_single_application_rooted_at_src()
    {
        var ws = NewDir();
        try
        {
            Dir(ws, "src", "app");

            var projects = new WorkspaceProjectLocator().Find(ws);

            var app = Assert.Single(projects);
            Assert.Equal("application", app.Type);
            Assert.Equal(ws, app.ProjectDir);
            Assert.Equal(Path.Combine(ws, "src"), app.SourceRoot);
        }
        finally
        {
            Directory.Delete(ws, recursive: true);
        }
    }

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Dir(string root, params string[] parts) =>
        Directory.CreateDirectory(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    private static void Write(string root, string relative, string content) =>
        File.WriteAllText(Path.Combine(root, relative), content);
}
