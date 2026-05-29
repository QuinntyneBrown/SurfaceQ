namespace SurfaceQ.Cli.Tests;

// Tests that invoke `dotnet pack -c Release` on SurfaceQ.Cli all build the same
// Release output (SurfaceQ.Core/Sidecar obj+bin). xUnit runs test classes in
// parallel by default, so concurrent packs collide writing those DLLs (CS2012:
// "being used by another process"). Sharing one collection makes them run
// sequentially relative to each other, removing the race.

[CollectionDefinition("Packaging", DisableParallelization = true)]
public sealed class PackagingCollection
{
}
