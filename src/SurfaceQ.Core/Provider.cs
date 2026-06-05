namespace SurfaceQ.Core;

// The result of generating a `provide-<project>.ts` Angular providers file.
// Content and TargetFile are empty and BindingCount is 0 when the library has no
// injectable contracts to wire (the command then skips it). Warnings carry
// non-fatal notes such as a token whose interface has no implementation.
public sealed record GeneratedProvider(
    string FunctionName,
    string TargetFile,
    string Content,
    int BindingCount,
    IReadOnlyList<string> Warnings);
