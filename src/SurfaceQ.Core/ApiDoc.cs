namespace SurfaceQ.Core;

// The structured public API of one library, as discovered by the sidecar and
// consumed by the MarkdownRenderer. Kept deliberately flat: each declaration
// carries only the fields its kind uses; unused fields stay empty.

public sealed record ApiParameter(string Name, string Type, bool Optional);

public sealed record ApiMember(
    string MemberKind,
    string Name,
    string Type,
    string ReturnType,
    bool Optional,
    bool Readonly,
    IReadOnlyList<ApiParameter> Parameters,
    string Doc);

public sealed record EnumMember(string Name, string Value, string Doc);

public sealed record ApiDeclaration(
    string Name,
    string Kind,
    string Doc,
    string Definition,
    string Contract,
    string Description,
    string Type,
    string ReturnType,
    IReadOnlyList<string> Extends,
    IReadOnlyList<string> Implements,
    IReadOnlyList<ApiParameter> Parameters,
    IReadOnlyList<ApiMember> Members,
    IReadOnlyList<EnumMember> EnumMembers);

public sealed record LibraryApi(string Name, IReadOnlyList<ApiDeclaration> Declarations);
