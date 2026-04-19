namespace SurfaceQ.Core;

public sealed class SourceFileWalker
{
    public IEnumerable<string> Walk(ProjectContext context)
    {
        return Directory.EnumerateFiles(context.ScanRoot, "*.ts", SearchOption.AllDirectories);
    }
}
