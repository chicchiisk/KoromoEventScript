namespace KoromoEventScript.Cli.Semantics;

public enum DefinitionKind
{
    Variable,
    Function,
    Class,
    Enum,
    EnumMember,
    Actor,
    Parameter,
    ClassField,
    ClassMethod,
}

public enum ScopeKind
{
    Module,
    Class,
    Enum,
    Function,
    Method,
    Block,
}

public sealed record DefinitionScope(
    string Id,
    ScopeKind Kind,
    string? ParentId,
    string? OwnerName);

public sealed record ScopedSymbolDefinition(
    string Name,
    DefinitionKind Kind,
    string ModuleName,
    string File,
    int Line,
    int Column,
    string ScopeId);

public sealed record DefinitionTable
{
    public DefinitionTable(
        string moduleScopeId,
        IReadOnlyList<DefinitionScope> scopes,
        IReadOnlyList<ScopedSymbolDefinition> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleScopeId);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(definitions);

        ModuleScopeId = moduleScopeId;
        Scopes = scopes.ToArray();
        Definitions = definitions.ToArray();
    }

    public string ModuleScopeId { get; }

    public IReadOnlyList<DefinitionScope> Scopes { get; }

    public IReadOnlyList<ScopedSymbolDefinition> Definitions { get; }
}
