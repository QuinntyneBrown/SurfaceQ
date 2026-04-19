// Acceptance Test
// Traces to: L2-005
// Description: Missing entryFile falls back to src/public-api.ts and logs info.

using SurfaceQ.Core;
using Xunit;

namespace SurfaceQ.Core.Tests;

public class ManifestReaderFallbackTests
{
    [Fact]
    public void Missing_entryFile_falls_back_and_logs_info()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = Path.Combine(dir, "ng-package.json");
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            File.WriteAllText(manifest, "{}");
            var messages = new List<string>();

            var context = new ManifestReader().Read(manifest, messages.Add);

            Assert.EndsWith(Path.Combine("src", "public-api.ts"), context.EntryFile);
            Assert.Single(messages);
            Assert.Contains("entryFile", messages[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
