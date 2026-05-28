using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Semantics;

public sealed class DefinitionCollector
{
    public DefinitionCollectionResult Collect(ScriptDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var symbols = new List<SymbolDefinition>();
        var diagnostics = new List<Diagnostic>();
        var scopes = new List<DefinitionScope>();
        var definitions = new List<ScopedSymbolDefinition>();
        var definitionsByScope = new Dictionary<string, Dictionary<string, ScopedSymbolDefinition>>(StringComparer.Ordinal);
        var legacyDefinitionsByName = new Dictionary<string, SymbolDefinition>(StringComparer.Ordinal);
        var moduleScope = CreateScopeId(document.ModuleName, ScopeKind.Module, null, null, scopes);

        foreach (var statement in document.Syntax.Statements)
        {
            CollectStatement(document, statement, moduleScope, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
        }

        return new DefinitionCollectionResult(
            document,
            symbols,
            diagnostics,
            new DefinitionTable(moduleScope, scopes, definitions));
    }

    private static void CollectStatement(
        ScriptDocument document,
        StatementSyntax statement,
        string scopeId,
        List<SymbolDefinition> symbols,
        List<ScopedSymbolDefinition> definitions,
        List<DefinitionScope> scopes,
        Dictionary<string, Dictionary<string, ScopedSymbolDefinition>> definitionsByScope,
        List<Diagnostic> diagnostics,
        Dictionary<string, SymbolDefinition> legacyDefinitionsByName)
    {
        switch (statement)
        {
            case VarStatementSyntax varStatement:
                AddScopedDefinition(
                    document,
                    definitions,
                    definitionsByScope,
                    scopes,
                    diagnostics,
                    scopeId,
                    varStatement.Name,
                    GetVariableKind(scopes, scopeId),
                    varStatement.NameLocation);
                if (IsModuleScope(scopes, scopeId))
                {
                    AddLegacySymbol(document, symbols, diagnostics, legacyDefinitionsByName, varStatement.Name, varStatement.NameLocation, reportDuplicate: false);
                }

                break;

            case FunctionDeclarationSyntax function:
                AddScopedDefinition(document, definitions, definitionsByScope, scopes, diagnostics, scopeId, function.Name, DefinitionKind.Function, function.NameLocation);
                if (IsModuleScope(scopes, scopeId))
                {
                    AddLegacySymbol(document, symbols, diagnostics, legacyDefinitionsByName, function.Name, function.NameLocation, reportDuplicate: false);
                }

                var functionScope = CreateScopeId(document.ModuleName, ScopeKind.Function, scopeId, function.Name, scopes);
                CollectParameters(document, function.Parameters, functionScope, definitions, definitionsByScope, scopes, diagnostics);
                CollectBlock(document, function.Body, functionScope, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                break;

            case ClassDeclarationSyntax classDeclaration:
                AddScopedDefinition(document, definitions, definitionsByScope, scopes, diagnostics, scopeId, classDeclaration.Name, DefinitionKind.Class, classDeclaration.NameLocation);
                if (IsModuleScope(scopes, scopeId))
                {
                    AddLegacySymbol(document, symbols, diagnostics, legacyDefinitionsByName, classDeclaration.Name, classDeclaration.NameLocation, reportDuplicate: false);
                }

                var classScope = CreateScopeId(document.ModuleName, ScopeKind.Class, scopeId, classDeclaration.Name, scopes);
                foreach (var member in classDeclaration.Members)
                {
                    CollectClassMember(document, member, classScope, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                }

                break;

            case EnumDeclarationSyntax enumDeclaration:
                AddScopedDefinition(document, definitions, definitionsByScope, scopes, diagnostics, scopeId, enumDeclaration.Name, DefinitionKind.Enum, enumDeclaration.NameLocation);
                if (IsModuleScope(scopes, scopeId))
                {
                    AddLegacySymbol(document, symbols, diagnostics, legacyDefinitionsByName, enumDeclaration.Name, enumDeclaration.NameLocation, reportDuplicate: false);
                }

                var enumScope = CreateScopeId(document.ModuleName, ScopeKind.Enum, scopeId, enumDeclaration.Name, scopes);
                foreach (var member in enumDeclaration.Members)
                {
                    AddScopedDefinition(document, definitions, definitionsByScope, scopes, diagnostics, enumScope, member.Name, DefinitionKind.EnumMember, member.NameLocation);
                }

                break;

            case ActorDeclarationSyntax actor:
                AddScopedDefinition(document, definitions, definitionsByScope, scopes, diagnostics, scopeId, actor.Name, DefinitionKind.Actor, actor.NameLocation);
                if (IsModuleScope(scopes, scopeId))
                {
                    AddLegacySymbol(document, symbols, diagnostics, legacyDefinitionsByName, actor.Name, actor.NameLocation, reportDuplicate: false);
                }

                var actorBlockScope = CreateScopeId(document.ModuleName, ScopeKind.Block, scopeId, actor.Name, scopes);
                CollectBlock(document, actor.Body, actorBlockScope, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                break;

            case LabelStatementSyntax labelStatement:
                AddLegacySymbol(document, symbols, diagnostics, legacyDefinitionsByName, labelStatement.Tag, labelStatement.TagLocation);
                break;

            case SayStatementSyntax { Tag: { Length: > 0 } tag, TagLocation: { } location }:
                AddLegacySymbol(document, symbols, diagnostics, legacyDefinitionsByName, tag, location);
                break;

            case NarStatementSyntax { Tag: { Length: > 0 } tag, TagLocation: { } location }:
                AddLegacySymbol(document, symbols, diagnostics, legacyDefinitionsByName, tag, location);
                break;

            case IfStatementSyntax ifStatement:
                CollectBlock(document, ifStatement.Body, scopeId, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                foreach (var elseIfClause in ifStatement.ElseIfClauses)
                {
                    CollectBlock(document, elseIfClause.Body, scopeId, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                }

                if (ifStatement.ElseBody is not null)
                {
                    CollectBlock(document, ifStatement.ElseBody, scopeId, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                }

                break;

            case WhileStatementSyntax whileStatement:
                CollectBlock(document, whileStatement.Body, scopeId, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                break;

            case ForStatementSyntax forStatement:
                AddScopedDefinition(
                    document,
                    definitions,
                    definitionsByScope,
                    scopes,
                    diagnostics,
                    scopeId,
                    forStatement.VariableName,
                    DefinitionKind.Variable,
                    forStatement.VariableLocation);
                CollectBlock(document, forStatement.Body, scopeId, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                break;
        }
    }

    private static void CollectClassMember(
        ScriptDocument document,
        ClassMemberSyntax member,
        string classScope,
        List<SymbolDefinition> symbols,
        List<ScopedSymbolDefinition> definitions,
        List<DefinitionScope> scopes,
        Dictionary<string, Dictionary<string, ScopedSymbolDefinition>> definitionsByScope,
        List<Diagnostic> diagnostics,
        Dictionary<string, SymbolDefinition> legacyDefinitionsByName)
    {
        switch (member)
        {
            case ClassFieldSyntax field:
                AddScopedDefinition(
                    document,
                    definitions,
                    definitionsByScope,
                    scopes,
                    diagnostics,
                    classScope,
                    field.Declaration.Name,
                    DefinitionKind.ClassField,
                    field.Declaration.NameLocation);
                break;

            case ClassMethodSyntax method:
                AddScopedDefinition(
                    document,
                    definitions,
                    definitionsByScope,
                    scopes,
                    diagnostics,
                    classScope,
                    method.Declaration.Name,
                    DefinitionKind.ClassMethod,
                    method.Declaration.NameLocation);
                var methodScope = CreateScopeId(document.ModuleName, ScopeKind.Method, classScope, method.Declaration.Name, scopes);
                CollectParameters(document, method.Declaration.Parameters, methodScope, definitions, definitionsByScope, scopes, diagnostics);
                CollectBlock(document, method.Declaration.Body, methodScope, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
                break;
        }
    }

    private static void CollectParameters(
        ScriptDocument document,
        IReadOnlyList<ParameterSyntax> parameters,
        string scopeId,
        List<ScopedSymbolDefinition> definitions,
        Dictionary<string, Dictionary<string, ScopedSymbolDefinition>> definitionsByScope,
        List<DefinitionScope> scopes,
        List<Diagnostic> diagnostics)
    {
        foreach (var parameter in parameters)
        {
            AddScopedDefinition(document, definitions, definitionsByScope, scopes, diagnostics, scopeId, parameter.Name, DefinitionKind.Parameter, parameter.NameLocation);
        }
    }

    private static void CollectBlock(
        ScriptDocument document,
        BlockSyntax block,
        string scopeId,
        List<SymbolDefinition> symbols,
        List<ScopedSymbolDefinition> definitions,
        List<DefinitionScope> scopes,
        Dictionary<string, Dictionary<string, ScopedSymbolDefinition>> definitionsByScope,
        List<Diagnostic> diagnostics,
        Dictionary<string, SymbolDefinition> legacyDefinitionsByName)
    {
        foreach (var statement in block.Statements)
        {
            CollectStatement(document, statement, scopeId, symbols, definitions, scopes, definitionsByScope, diagnostics, legacyDefinitionsByName);
        }
    }

    private static void AddScopedDefinition(
        ScriptDocument document,
        List<ScopedSymbolDefinition> definitions,
        Dictionary<string, Dictionary<string, ScopedSymbolDefinition>> definitionsByScope,
        List<DefinitionScope> scopes,
        List<Diagnostic> diagnostics,
        string scopeId,
        string name,
        DefinitionKind kind,
        SourceLocation location)
    {
        var definition = new ScopedSymbolDefinition(name, kind, document.ModuleName, document.ProjectRelativePath, location.Line, location.Column, scopeId);
        definitions.Add(definition);

        if (!definitionsByScope.TryGetValue(scopeId, out var scopeDefinitions))
        {
            scopeDefinitions = new Dictionary<string, ScopedSymbolDefinition>(StringComparer.Ordinal);
            definitionsByScope[scopeId] = scopeDefinitions;
        }

        if (!scopeDefinitions.TryAdd(name, definition))
        {
            diagnostics.Add(DuplicateDefinitionDiagnostic(document, scopeDefinitions[name], definition));
            return;
        }

        if (FindInOuterScopes(scopes, definitionsByScope, scopeId, name) is not null)
        {
            diagnostics.Add(ShadowingDiagnostic(document, definition));
        }
    }

    private static ScopedSymbolDefinition? FindInOuterScopes(
        IReadOnlyList<DefinitionScope> scopes,
        Dictionary<string, Dictionary<string, ScopedSymbolDefinition>> definitionsByScope,
        string scopeId,
        string name)
    {
        var scope = scopes.Single(current => string.Equals(current.Id, scopeId, StringComparison.Ordinal));
        while (scope.ParentId is { } parentId)
        {
            if (definitionsByScope.TryGetValue(parentId, out var definitions) &&
                definitions.TryGetValue(name, out var definition))
            {
                return definition;
            }

            scope = scopes.Single(current => string.Equals(current.Id, parentId, StringComparison.Ordinal));
        }

        return null;
    }

    private static void AddLegacySymbol(
        ScriptDocument document,
        List<SymbolDefinition> symbols,
        List<Diagnostic> diagnostics,
        Dictionary<string, SymbolDefinition> firstDefinitionsByName,
        string name,
        SourceLocation location,
        bool reportDuplicate = true)
    {
        var symbol = new SymbolDefinition(name, document.ModuleName, document.ProjectRelativePath, location.Line, location.Column);
        symbols.Add(symbol);

        if (!firstDefinitionsByName.TryAdd(symbol.Name, symbol) && reportDuplicate)
        {
            diagnostics.Add(DuplicateDefinitionDiagnostic(document, firstDefinitionsByName[symbol.Name], symbol));
        }
    }

    private static string CreateScopeId(
        string moduleName,
        ScopeKind kind,
        string? parentId,
        string? ownerName,
        List<DefinitionScope> scopes)
    {
        var id = $"{moduleName}:{kind}:{scopes.Count}";
        scopes.Add(new DefinitionScope(id, kind, parentId, ownerName));
        return id;
    }

    private static bool IsModuleScope(IReadOnlyList<DefinitionScope> scopes, string scopeId)
    {
        return scopes.Single(scope => string.Equals(scope.Id, scopeId, StringComparison.Ordinal)).Kind == ScopeKind.Module;
    }

    private static DefinitionKind GetVariableKind(IReadOnlyList<DefinitionScope> scopes, string scopeId)
    {
        return scopes.Single(scope => string.Equals(scope.Id, scopeId, StringComparison.Ordinal)).Kind == ScopeKind.Class
            ? DefinitionKind.ClassField
            : DefinitionKind.Variable;
    }

    private static Diagnostic DuplicateDefinitionDiagnostic(ScriptDocument document, SymbolDefinition original, SymbolDefinition duplicate)
    {
        return new Diagnostic(
            DiagnosticLevel.Error,
            "KES2009",
            duplicate.File,
            duplicate.Line,
            duplicate.Column,
            $"Duplicate top-level definition '{duplicate.Name}' in module '{document.ModuleName}'.",
            [OriginalDefinitionLocation(original)]);
    }

    private static Diagnostic DuplicateDefinitionDiagnostic(
        ScriptDocument document,
        ScopedSymbolDefinition original,
        ScopedSymbolDefinition duplicate)
    {
        return new Diagnostic(
            DiagnosticLevel.Error,
            "KES2009",
            duplicate.File,
            duplicate.Line,
            duplicate.Column,
            $"Duplicate definition '{duplicate.Name}' in module '{document.ModuleName}'.",
            [OriginalDefinitionLocation(original)]);
    }

    private static Diagnostic ShadowingDiagnostic(ScriptDocument document, ScopedSymbolDefinition duplicate)
    {
        return new Diagnostic(
            DiagnosticLevel.Error,
            "KES2014",
            duplicate.File,
            duplicate.Line,
            duplicate.Column,
            $"Definition '{duplicate.Name}' in module '{document.ModuleName}' shadows an outer scope definition.");
    }

    private static DiagnosticRelatedLocation OriginalDefinitionLocation(SymbolDefinition original)
    {
        return new DiagnosticRelatedLocation(
            original.File,
            original.Line,
            original.Column,
            "Original definition is here.");
    }

    private static DiagnosticRelatedLocation OriginalDefinitionLocation(ScopedSymbolDefinition original)
    {
        return new DiagnosticRelatedLocation(
            original.File,
            original.Line,
            original.Column,
            "Original definition is here.");
    }
}
