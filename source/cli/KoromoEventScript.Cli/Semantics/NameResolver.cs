using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Lexing;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Semantics;

public sealed class NameResolver
{
    private static readonly HashSet<string> BuiltInCallables = new(StringComparer.Ordinal)
    {
        "array_len",
        "assert",
        "autosave",
        "bg",
        "bgm",
        "bgm_stop",
        "bool_to_string",
        "camera_autofocus",
        "cm",
        "face",
        "get_config",
        "get_param",
        "hide",
        "is_read",
        "l",
        "load",
        "mark_read",
        "move",
        "nar",
        "number_to_string",
        "p",
        "print",
        "r",
        "range",
        "rt_back",
        "rt_front",
        "save",
        "se",
        "se_stop_all",
        "se_stop",
        "set_auto",
        "set_config_bool",
        "set_config_number",
        "set_config_string",
        "set_param_bool",
        "set_param_number",
        "set_param_string",
        "set_skip",
        "show",
        "standby",
        "str_len",
        "trans",
        "vf",
        "vo",
        "wait",
        "voice_stop",
        "wait_click",
        "action_jump",
    };

    private static readonly HashSet<string> ActorFirstArgumentCallables = new(StringComparer.Ordinal)
    {
        "action_jump",
        "face",
        "hide",
        "move",
        "show",
        "standby",
        "vf",
    };

    public NameResolutionResult ResolveNames(
        ImportGraph graph,
        IReadOnlyList<DefinitionCollectionResult> definitionCollections)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(definitionCollections);

        var context = ResolutionContext.From(definitionCollections, strictReferenceKinds: true);
        var diagnostics = new List<Diagnostic>();

        foreach (var document in graph.OrderedDocuments)
        {
            if (!context.DocumentsByModule.TryGetValue(document.ModuleName, out var documentContext))
            {
                continue;
            }

            diagnostics.AddRange(DetectLocalImportCollisions(document, documentContext, graph, context));
            diagnostics.AddRange(ResolveReferences(document, documentContext, graph, context));
        }

        return diagnostics.Count == 0
            ? NameResolutionResult.Success(context.SymbolsByModule)
            : NameResolutionResult.Failure(CliExitCode.CompileError, diagnostics);
    }

    public NameResolutionResult ResolveNames(
        ImportGraph graph,
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> symbolsByModule)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(symbolsByModule);

        var collections = graph.OrderedDocuments
            .Select(document => new DefinitionCollectionResult(
                document,
                GetSymbols(symbolsByModule, document.ModuleName),
                []))
            .ToArray();
        var context = ResolutionContext.From(collections, strictReferenceKinds: false);
        var diagnostics = new List<Diagnostic>();

        foreach (var document in graph.OrderedDocuments)
        {
            if (!context.DocumentsByModule.TryGetValue(document.ModuleName, out var documentContext))
            {
                continue;
            }

            diagnostics.AddRange(DetectLocalImportCollisions(document, documentContext, graph, context));
            diagnostics.AddRange(ResolveReferences(document, documentContext, graph, context));
        }

        return diagnostics.Count == 0
            ? NameResolutionResult.Success(context.SymbolsByModule)
            : NameResolutionResult.Failure(CliExitCode.CompileError, diagnostics);
    }

    private static IEnumerable<Diagnostic> DetectLocalImportCollisions(
        ScriptDocument document,
        DocumentResolutionContext documentContext,
        ImportGraph graph,
        ResolutionContext context)
    {
        foreach (var localDefinition in documentContext.ModuleDefinitions.Where(static definition => !IsTagName(definition.Name)))
        {
            var collisions = GetReachableModuleDefinitions(graph, context, document.ModuleName)
                .Where(importedDefinition => string.Equals(importedDefinition.Name, localDefinition.Name, StringComparison.Ordinal))
                .ToArray();
            if (collisions.Length == 0)
            {
                continue;
            }

            yield return new Diagnostic(
                DiagnosticLevel.Error,
                "KES2011",
                localDefinition.File,
                localDefinition.Line,
                localDefinition.Column,
                $"Local definition '{localDefinition.Name}' in module '{document.ModuleName}' conflicts with imported definition from {FormatModules(collisions)}.");
        }
    }

    private static IEnumerable<Diagnostic> ResolveReferences(
        ScriptDocument document,
        DocumentResolutionContext documentContext,
        ImportGraph graph,
        ResolutionContext context)
    {
        foreach (var reference in CollectReferences(documentContext, document.Syntax))
        {
            if (reference.Kind == ReferenceKind.Label)
            {
                if (!documentContext.LocalTags.Contains(reference.Name))
                {
                    yield return UndefinedTagDiagnostic(reference);
                }

                continue;
            }

            var resolution = ResolveReference(reference, document.ModuleName, graph, context);
            switch (resolution.Status)
            {
                case ReferenceResolutionStatus.Resolved:
                    break;

                case ReferenceResolutionStatus.Ambiguous:
                    yield return new Diagnostic(
                        DiagnosticLevel.Error,
                        "KES2012",
                        reference.File,
                        reference.Line,
                        reference.Column,
                        $"Reference '{reference.Name}' is ambiguous between imported definitions from {FormatModules(resolution.ImportedMatches)}.");
                    break;

                case ReferenceResolutionStatus.Undefined:
                    yield return UndefinedNameDiagnostic(reference);
                    break;
            }
        }
    }

    private static ReferenceResolution ResolveReference(
        Reference reference,
        string moduleName,
        ImportGraph graph,
        ResolutionContext context)
    {
        if (reference.Kind == ReferenceKind.Function && BuiltInCallables.Contains(reference.Name))
        {
            return ReferenceResolution.Resolved();
        }

        var documentContext = context.DocumentsByModule[moduleName];
        if (ResolveLocal(reference, documentContext))
        {
            return ReferenceResolution.Resolved();
        }

        var importedMatches = GetReachableModuleDefinitions(graph, context, moduleName)
            .Where(definition => string.Equals(definition.Name, reference.Name, StringComparison.Ordinal))
            .Where(definition => IsAllowedKind(reference.Kind, definition.Kind))
            .ToArray();

        return importedMatches.Length switch
        {
            0 => ReferenceResolution.Undefined(),
            1 => ReferenceResolution.Resolved(),
            _ => ReferenceResolution.Ambiguous(importedMatches),
        };
    }

    private static bool ResolveLocal(Reference reference, DocumentResolutionContext documentContext)
    {
        var scope = documentContext.FindScope(reference.ScopeId);
        while (scope is not null)
        {
            if (documentContext.DefinitionsByScope.TryGetValue(scope.Id, out var definitions) &&
                definitions.TryGetValue(reference.Name, out var definition) &&
                IsAllowedKind(reference.Kind, definition.Kind))
            {
                return true;
            }

            scope = scope.ParentId is null ? null : documentContext.FindScope(scope.ParentId);
        }

        return false;
    }

    private static bool IsAllowedKind(ReferenceKind referenceKind, DefinitionKind definitionKind)
    {
        return referenceKind switch
        {
            ReferenceKind.Variable => definitionKind is DefinitionKind.Variable
                or DefinitionKind.Parameter
                or DefinitionKind.ClassField
                or DefinitionKind.EnumMember,
            ReferenceKind.Actor => definitionKind is DefinitionKind.Actor,
            ReferenceKind.Function => definitionKind is DefinitionKind.Function or DefinitionKind.ClassMethod,
            _ => false,
        };
    }

    private static IEnumerable<Reference> CollectReferences(
        DocumentResolutionContext documentContext,
        ScriptSyntax syntax)
    {
        foreach (var statement in syntax.Statements)
        {
            foreach (var reference in CollectReferences(documentContext, statement, documentContext.DefinitionTable.ModuleScopeId))
            {
                yield return reference;
            }
        }
    }

    private static IEnumerable<Reference> CollectReferences(
        DocumentResolutionContext documentContext,
        StatementSyntax statement,
        string scopeId)
    {
        switch (statement)
        {
            case VarStatementSyntax varStatement:
                foreach (var reference in FromExpressionTokens(documentContext, varStatement.ValueTokens, scopeId))
                {
                    yield return reference;
                }

                break;

            case AssignmentStatementSyntax assignment:
                yield return new Reference(ReferenceKind.Variable, assignment.TargetName, documentContext.Document.ProjectRelativePath, assignment.TargetLocation.Line, assignment.TargetLocation.Column, scopeId);
                if (assignment.IndexTokens is not null)
                {
                    foreach (var reference in FromExpressionTokens(documentContext, assignment.IndexTokens, scopeId))
                    {
                        yield return reference;
                    }
                }

                foreach (var reference in FromExpressionTokens(documentContext, assignment.ValueTokens, scopeId))
                {
                    yield return reference;
                }

                break;

            case FunctionDeclarationSyntax function:
                if (documentContext.TryFindChildScope(scopeId, ScopeKind.Function, function.Name, out var functionScope))
                {
                    foreach (var reference in CollectReferences(documentContext, function.Body, functionScope.Id))
                    {
                        yield return reference;
                    }
                }

                break;

            case ActorDeclarationSyntax actor:
                if (documentContext.TryFindChildScope(scopeId, ScopeKind.Block, actor.Name, out var actorScope))
                {
                    foreach (var reference in CollectReferences(documentContext, actor.Body, actorScope.Id))
                    {
                        yield return reference;
                    }
                }

                break;

            case StandbyStatementSyntax standbyStatement:
                foreach (var entry in standbyStatement.Entries)
                {
                    yield return new Reference(ReferenceKind.Actor, entry.ActorTypeName, documentContext.Document.ProjectRelativePath, entry.ActorTypeLocation.Line, entry.ActorTypeLocation.Column, scopeId);
                }

                break;

            case ClassDeclarationSyntax classDeclaration:
                if (documentContext.TryFindChildScope(scopeId, ScopeKind.Class, classDeclaration.Name, out var classScope))
                {
                    foreach (var member in classDeclaration.Members)
                    {
                        foreach (var reference in CollectClassMemberReferences(documentContext, member, classScope.Id))
                        {
                            yield return reference;
                        }
                    }
                }

                break;

            case JumpStatementSyntax jumpStatement:
                yield return new Reference(ReferenceKind.Label, jumpStatement.Tag, documentContext.Document.ProjectRelativePath, jumpStatement.TagLocation.Line, jumpStatement.TagLocation.Column, scopeId);
                break;

            case CommandStatementSyntax commandStatement:
                if (documentContext.StrictReferenceKinds)
                {
                    yield return new Reference(ReferenceKind.Function, commandStatement.Name, documentContext.Document.ProjectRelativePath, commandStatement.NameLocation.Line, commandStatement.NameLocation.Column, scopeId);
                }

                foreach (var reference in FromCommandArguments(documentContext, commandStatement.Name, commandStatement.Arguments, scopeId))
                {
                    yield return reference;
                }

                break;

            case LessStatementSyntax lessStatement:
                foreach (var reference in FromLessStatement(documentContext, lessStatement, scopeId, documentContext.StrictReferenceKinds))
                {
                    yield return reference;
                }

                break;

            case SayStatementSyntax sayStatement:
                yield return new Reference(ReferenceKind.Variable, sayStatement.Speaker, documentContext.Document.ProjectRelativePath, sayStatement.SpeakerLocation.Line, sayStatement.SpeakerLocation.Column, scopeId);

                break;

            case SelectStatementSyntax selectStatement:
                foreach (var caseClause in selectStatement.Cases)
                {
                    yield return new Reference(ReferenceKind.Label, caseClause.Tag, documentContext.Document.ProjectRelativePath, caseClause.TagLocation.Line, caseClause.TagLocation.Column, scopeId);
                }

                break;

            case IfStatementSyntax ifStatement:
                foreach (var reference in FromExpressionTokens(documentContext, ifStatement.ConditionTokens, scopeId))
                {
                    yield return reference;
                }

                foreach (var reference in CollectReferences(documentContext, ifStatement.Body, scopeId))
                {
                    yield return reference;
                }

                foreach (var elseIfClause in ifStatement.ElseIfClauses)
                {
                    foreach (var reference in FromExpressionTokens(documentContext, elseIfClause.ConditionTokens, scopeId))
                    {
                        yield return reference;
                    }

                    foreach (var reference in CollectReferences(documentContext, elseIfClause.Body, scopeId))
                    {
                        yield return reference;
                    }
                }

                if (ifStatement.ElseBody is not null)
                {
                    foreach (var reference in CollectReferences(documentContext, ifStatement.ElseBody, scopeId))
                    {
                        yield return reference;
                    }
                }

                break;

            case WhileStatementSyntax whileStatement:
                foreach (var reference in FromExpressionTokens(documentContext, whileStatement.ConditionTokens, scopeId))
                {
                    yield return reference;
                }

                foreach (var reference in CollectReferences(documentContext, whileStatement.Body, scopeId))
                {
                    yield return reference;
                }

                break;

            case ForStatementSyntax forStatement:
                foreach (var reference in FromExpressionTokens(documentContext, forStatement.IterableTokens, scopeId))
                {
                    yield return reference;
                }

                foreach (var reference in CollectReferences(documentContext, forStatement.Body, scopeId))
                {
                    yield return reference;
                }

                break;
        }
    }

    private static IEnumerable<Reference> CollectReferences(
        DocumentResolutionContext documentContext,
        BlockSyntax block,
        string scopeId)
    {
        foreach (var statement in block.Statements)
        {
            foreach (var reference in CollectReferences(documentContext, statement, scopeId))
            {
                yield return reference;
            }
        }
    }

    private static IEnumerable<Reference> CollectClassMemberReferences(
        DocumentResolutionContext documentContext,
        ClassMemberSyntax member,
        string classScopeId)
    {
        switch (member)
        {
            case ClassFieldSyntax field:
                foreach (var reference in CollectReferences(documentContext, field.Declaration, classScopeId))
                {
                    yield return reference;
                }

                break;

            case ClassMethodSyntax method:
                if (documentContext.TryFindChildScope(classScopeId, ScopeKind.Method, method.Declaration.Name, out var methodScope))
                {
                    foreach (var reference in CollectReferences(documentContext, method.Declaration.Body, methodScope.Id))
                    {
                        yield return reference;
                    }
                }

                break;
        }
    }

    private static IEnumerable<Reference> FromLessStatement(
        DocumentResolutionContext documentContext,
        LessStatementSyntax statement,
        string scopeId,
        bool strictReferenceKinds)
    {
        if (strictReferenceKinds)
        {
            yield return new Reference(ReferenceKind.Function, statement.Name, documentContext.Document.ProjectRelativePath, statement.NameLocation.Line, statement.NameLocation.Column, scopeId);
        }
        foreach (var reference in FromCommandArguments(documentContext, statement.Name, statement.SharedArguments, scopeId))
        {
            yield return reference;
        }

        foreach (var item in statement.Items)
        {
            switch (item)
            {
                case LessCommandItemSyntax commandItem:
                    foreach (var reference in FromCommandArguments(documentContext, statement.Name, commandItem.Arguments, scopeId))
                    {
                        yield return reference;
                    }

                    break;

                case LessNestedStatementSyntax nestedStatement:
                    foreach (var reference in FromLessStatement(documentContext, nestedStatement.Statement, scopeId, strictReferenceKinds))
                    {
                        yield return reference;
                    }

                    break;
            }
        }
    }

    private static IEnumerable<Reference> FromCommandArguments(
        DocumentResolutionContext documentContext,
        string commandName,
        IReadOnlyList<Token> tokens,
        string scopeId)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind != TokenKind.Identifier)
            {
                continue;
            }

            if (index > 0 &&
                tokens[index - 1].Kind == TokenKind.Keyword &&
                string.Equals(tokens[index - 1].Lexeme, "new", StringComparison.Ordinal))
            {
                continue;
            }

            if (index > 0 && tokens[index - 1].Kind == TokenKind.Dot)
            {
                continue;
            }

            if (index == 0 && ActorFirstArgumentCallables.Contains(commandName))
            {
                yield return new Reference(ReferenceKind.Variable, token.Lexeme, documentContext.Document.ProjectRelativePath, token.Line, token.Column, scopeId);
                continue;
            }

            var kind = IsFunctionCallToken(documentContext, tokens, index) ? ReferenceKind.Function : ReferenceKind.Variable;
            yield return new Reference(kind, token.Lexeme, documentContext.Document.ProjectRelativePath, token.Line, token.Column, scopeId);
        }
    }

    private static IEnumerable<Reference> FromExpressionTokens(
        DocumentResolutionContext documentContext,
        IReadOnlyList<Token> tokens,
        string scopeId)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Kind != TokenKind.Identifier)
            {
                continue;
            }

            if (index > 0 &&
                tokens[index - 1].Kind == TokenKind.Keyword &&
                string.Equals(tokens[index - 1].Lexeme, "new", StringComparison.Ordinal))
            {
                continue;
            }

            if (index > 0 && tokens[index - 1].Kind == TokenKind.Dot)
            {
                continue;
            }

            var kind = IsFunctionCallToken(documentContext, tokens, index) ? ReferenceKind.Function : ReferenceKind.Variable;
            yield return new Reference(kind, token.Lexeme, documentContext.Document.ProjectRelativePath, token.Line, token.Column, scopeId);
        }
    }

    private static bool IsFunctionCallToken(DocumentResolutionContext documentContext, IReadOnlyList<Token> tokens, int index)
    {
        if (index + 1 >= tokens.Count)
        {
            return false;
        }

        var token = tokens[index];
        if (tokens[index + 1].Kind == TokenKind.OpenParen)
        {
            return IsCallableName(documentContext, token.Lexeme);
        }

        return IsCallableName(documentContext, token.Lexeme) &&
            tokens[index + 1].Kind is TokenKind.Identifier or TokenKind.Keyword or TokenKind.StringLiteral or TokenKind.NumberLiteral or TokenKind.OpenBracket;
    }

    private static bool IsCallableName(DocumentResolutionContext documentContext, string name)
    {
        if (BuiltInCallables.Contains(name))
        {
            return true;
        }

        return documentContext.DefinitionTable.Definitions.Any(definition =>
            string.Equals(definition.Name, name, StringComparison.Ordinal) &&
            definition.Kind is DefinitionKind.Function or DefinitionKind.ClassMethod);
    }

    private static IEnumerable<ScopedSymbolDefinition> GetReachableModuleDefinitions(
        ImportGraph graph,
        ResolutionContext context,
        string moduleName)
    {
        return graph.GetReachableImports(moduleName)
            .SelectMany(importedModule => context.DocumentsByModule.TryGetValue(importedModule, out var importedContext)
                ? importedContext.ModuleDefinitions
                : []);
    }

    private static IReadOnlyList<SymbolDefinition> GetSymbols(
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> symbolsByModule,
        string moduleName)
    {
        return symbolsByModule.TryGetValue(moduleName, out var symbols)
            ? symbols
            : [];
    }

    private static Diagnostic UndefinedNameDiagnostic(Reference reference)
    {
        return new Diagnostic(
            DiagnosticLevel.Error,
            "KES2010",
            reference.File,
            reference.Line,
            reference.Column,
            reference.Kind switch
            {
                ReferenceKind.Actor => $"Undefined actor '{reference.Name}'.",
                ReferenceKind.Function => $"Undefined function '{reference.Name}'.",
                _ => $"Undefined name '{reference.Name}'.",
            });
    }

    private static Diagnostic UndefinedTagDiagnostic(Reference reference)
    {
        return new Diagnostic(
            DiagnosticLevel.Error,
            "KES2013",
            reference.File,
            reference.Line,
            reference.Column,
            $"Undefined tag '{reference.Name}'.");
    }

    private static string FormatModules(IEnumerable<ScopedSymbolDefinition> definitions)
    {
        return string.Join(", ", definitions
            .Select(static definition => definition.ModuleName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));
    }

    private static bool IsTagName(string name)
    {
        return name.StartsWith('#');
    }

    private enum ReferenceKind
    {
        Variable,
        Actor,
        Function,
        Label,
    }

    private enum ReferenceResolutionStatus
    {
        Resolved,
        Undefined,
        Ambiguous,
    }

    private sealed record Reference(
        ReferenceKind Kind,
        string Name,
        string File,
        int Line,
        int Column,
        string ScopeId);

    private sealed record ReferenceResolution(
        ReferenceResolutionStatus Status,
        IReadOnlyList<ScopedSymbolDefinition> ImportedMatches)
    {
        public static ReferenceResolution Resolved()
        {
            return new ReferenceResolution(ReferenceResolutionStatus.Resolved, []);
        }

        public static ReferenceResolution Undefined()
        {
            return new ReferenceResolution(ReferenceResolutionStatus.Undefined, []);
        }

        public static ReferenceResolution Ambiguous(IReadOnlyList<ScopedSymbolDefinition> importedMatches)
        {
            return new ReferenceResolution(ReferenceResolutionStatus.Ambiguous, importedMatches);
        }
    }

    private sealed record ResolutionContext(
        IReadOnlyDictionary<string, DocumentResolutionContext> DocumentsByModule,
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> SymbolsByModule)
    {
        public static ResolutionContext From(IReadOnlyList<DefinitionCollectionResult> definitionCollections, bool strictReferenceKinds)
        {
            var documentsByModule = definitionCollections
                .GroupBy(static collection => collection.Document.ModuleName, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    group => new DocumentResolutionContext(group.First().Document, MergeTables(group), group.SelectMany(static collection => collection.Symbols).ToArray(), strictReferenceKinds),
                    StringComparer.Ordinal);
            var symbolsByModule = definitionCollections
                .GroupBy(static collection => collection.Document.ModuleName, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => (IReadOnlyList<SymbolDefinition>)group.SelectMany(static collection => collection.Symbols).ToArray(),
                    StringComparer.Ordinal);

            return new ResolutionContext(documentsByModule, symbolsByModule);
        }

        private static DefinitionTable MergeTables(IEnumerable<DefinitionCollectionResult> results)
        {
            var resultArray = results.ToArray();
            var firstTable = resultArray[0].DefinitionTable;
            if (resultArray.Length == 1)
            {
                return firstTable;
            }

            return new DefinitionTable(
                firstTable.ModuleScopeId,
                resultArray.SelectMany(static result => result.DefinitionTable.Scopes).ToArray(),
                resultArray.SelectMany(static result => result.DefinitionTable.Definitions).ToArray());
        }
    }

    private sealed class DocumentResolutionContext
    {
        private readonly Dictionary<string, DefinitionScope> scopesById;
        private readonly Dictionary<string, List<DefinitionScope>> childScopesByParent;

        public DocumentResolutionContext(
            ScriptDocument document,
            DefinitionTable definitionTable,
            IReadOnlyList<SymbolDefinition> symbols)
            : this(document, definitionTable, symbols, strictReferenceKinds: true)
        {
        }

        public DocumentResolutionContext(
            ScriptDocument document,
            DefinitionTable definitionTable,
            IReadOnlyList<SymbolDefinition> symbols,
            bool strictReferenceKinds)
        {
            Document = document;
            DefinitionTable = definitionTable;
            Symbols = symbols;
            StrictReferenceKinds = strictReferenceKinds;
            scopesById = definitionTable.Scopes.ToDictionary(static scope => scope.Id, StringComparer.Ordinal);
            childScopesByParent = definitionTable.Scopes
                .Where(static scope => scope.ParentId is not null)
                .GroupBy(static scope => scope.ParentId!, StringComparer.Ordinal)
                .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.Ordinal);
            DefinitionsByScope = definitionTable.Definitions
                .GroupBy(static definition => definition.ScopeId, StringComparer.Ordinal)
                .ToDictionary(
                    static group => group.Key,
                    static group => group
                        .GroupBy(static definition => definition.Name, StringComparer.Ordinal)
                        .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal),
                    StringComparer.Ordinal);
            ModuleDefinitions = definitionTable.Definitions
                .Where(definition => string.Equals(definition.ScopeId, definitionTable.ModuleScopeId, StringComparison.Ordinal))
                .ToArray();
            LocalTags = symbols
                .Where(static symbol => IsTagName(symbol.Name))
                .Select(static symbol => symbol.Name)
                .ToHashSet(StringComparer.Ordinal);
        }

        public ScriptDocument Document { get; }

        public DefinitionTable DefinitionTable { get; }

        public IReadOnlyList<SymbolDefinition> Symbols { get; }

        public bool StrictReferenceKinds { get; }

        public IReadOnlyDictionary<string, Dictionary<string, ScopedSymbolDefinition>> DefinitionsByScope { get; }

        public IReadOnlyList<ScopedSymbolDefinition> ModuleDefinitions { get; }

        public HashSet<string> LocalTags { get; }

        public DefinitionScope? FindScope(string scopeId)
        {
            return scopesById.GetValueOrDefault(scopeId);
        }

        public bool TryFindChildScope(string parentScopeId, ScopeKind kind, string? ownerName, out DefinitionScope scope)
        {
            if (childScopesByParent.TryGetValue(parentScopeId, out var childScopes))
            {
                var match = childScopes.FirstOrDefault(candidate =>
                    candidate.Kind == kind &&
                    string.Equals(candidate.OwnerName, ownerName, StringComparison.Ordinal));
                if (match is not null)
                {
                    scope = match;
                    return true;
                }
            }

            scope = null!;
            return false;
        }
    }
}
