namespace SurfaceQ.Core;

// The model for the `surfaceq inventory` command: a flat census of every
// developer-authored declaration in one project. Kept separate from ApiDoc
// (the docs command's contract model) because an inventory is a different
// shape — every declaration, exported or not, classified by Angular role.

// One Angular project (application or library) discovered in a workspace.
// SourceRoot is the directory whose `.ts` files are walked; ProjectDir is the
// project root the output file and reported paths are relative to.
public sealed record InventoryProject(
    string Name,
    string Type,
    string ProjectDir,
    string SourceRoot);

// One developer-authored declaration. File is project-relative POSIX text.
public sealed record InventoryItem(
    string Name,
    string Kind,
    string Role,
    bool Exported,
    string File,
    string Doc,
    bool Deprecated,
    string DeprecationReason);

// Everything inventoried for one project, ready to render.
public sealed record ProjectInventory(
    string Name,
    string Type,
    IReadOnlyList<InventoryItem> Items);
