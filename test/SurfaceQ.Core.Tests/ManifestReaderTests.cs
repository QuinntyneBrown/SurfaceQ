// Acceptance Test
// Traces to: L2-005
// Description: ManifestReader resolves entryFile relative to the manifest dir.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class ManifestReaderTests
{
    [Fact]
    public void Reads_entryFile_relative_to_manifest_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(manifest, "{ \"entryFile\": \"src/public-api.ts\" }");

            var context = new ManifestReader().Read(manifest);

            Assert.Equal(Path.GetFullPath(Path.Combine(dir, "src/public-api.ts")), context.EntryFile);
            Assert.Equal(Path.GetFullPath(Path.Combine(dir, "src")), context.ScanRoot);
            Assert.Equal(manifest, context.ManifestPath);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
