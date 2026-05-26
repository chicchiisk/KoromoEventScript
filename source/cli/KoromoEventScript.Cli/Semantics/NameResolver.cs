using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Lexing;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Semantics;

public sealed class NameResolver
{
    public NameResolutionResult ResolveNames(
        ImportGraph graph,
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> symbolsByModule)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(symbolsByModule);

        var diagnostics = new List<Diagnostic>();
        var normalizedSymbols = CopySymbols(symbolsByModule);

        foreach (var document in graph.OrderedDocuments)
        {
            var localSymbols = GetSymbols(normalizedSymbols, document.ModuleName);
            var reachableSymbols = graph.GetReachableImports(document.ModuleName)
                .SelectMany(moduleName => GetSymbols(normalizedSymbols, moduleName))
                .ToArray();

            diagnostics.AddRange(DetectLocalImportCollisions(document, localSymbols, reachableSymbols));
            diagnostics.AddRange(ResolveReferences(document, localSymbols, reachableSymbols));
            diagnostics.AddRange(ResolveTagReferences(document, localSymbols));
        }

        return diagnostics.Count == 0
            ? NameResolutionResult.Success(normalizedSymbols)
            : NameResolutionResult.Failure(CliExitCode.CompileError, diagnostics);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> CopySymbols(
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> symbolsByModule)
    {
        return symbolsByModule.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray() as IReadOnlyList<SymbolDefinition>,
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<SymbolDefinition> GetSymbols(
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> symbolsByModule,
        string moduleName)
    {
        return symbolsByModule.TryGetValue(moduleName, out var symbols)
            ? symbols
            : [];
    }

    private static IEnumerable<Diagnostic> DetectLocalImportCollisions(
        ScriptDocument document,
        IReadOnlyList<SymbolDefinition> localSymbols,
        IReadOnlyList<SymbolDefinition> reachableSymbols)
    {
        foreach (var localSymbol in localSymbols)
        {
            if (IsTagName(localSymbol.Name))
            {
                continue;
            }

            var collisions = reachableSymbols
                .Where(importedSymbol => string.Equals(importedSymbol.Name, localSymbol.Name, StringComparison.Ordinal))
                .ToArray();
            if (collisions.Length == 0)
            {
                continue;
            }

            yield return new Diagnostic(
                DiagnosticLevel.Error,
                "KES2011",
                localSymbol.File,
                localSymbol.Line,
                localSymbol.Column,
                $"Local definition '{localSymbol.Name}' in module '{document.ModuleName}' conflicts with imported definition from {FormatModules(collisions)}.");
        }
    }

    private static IEnumerable<Diagnostic> ResolveTagReferences(
        ScriptDocument document,
        IReadOnlyList<SymbolDefinition> localSymbols)
    {
        var localTags = localSymbols
            .Where(static symbol => IsTagName(symbol.Name))
            .Select(static symbol => symbol.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var reference in GetTagReferences(document.Syntax))
        {
            if (localTags.Contains(reference.Name))
            {
                continue;
            }

            yield return new Diagnostic(
                DiagnosticLevel.Error,
                "KES2013",
                document.ProjectRelativePath,
                reference.Line,
                reference.Column,
                $"Undefined tag '{reference.Name}'.");
        }
    }

    private static IEnumerable<Diagnostic> ResolveReferences(
        ScriptDocument document,
        IReadOnlyList<SymbolDefinition> localSymbols,
        IReadOnlyList<SymbolDefinition> reachableSymbols)
    {
        var localNames = localSymbols
            .Select(static symbol => symbol.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var reference in GetIdentifierReferences(document.Syntax))
        {
            if (localNames.Contains(reference.Name))
            {
                continue;
            }

            var importedMatches = reachableSymbols
                .Where(symbol => string.Equals(symbol.Name, reference.Name, StringComparison.Ordinal))
                .ToArray();

            if (importedMatches.Length == 1)
            {
                continue;
            }

            if (importedMatches.Length > 1)
            {
                yield return new Diagnostic(
                    DiagnosticLevel.Error,
                    "KES2012",
                    document.ProjectRelativePath,
                    reference.Line,
                    reference.Column,
                    $"Reference '{reference.Name}' is ambiguous between imported definitions from {FormatModules(importedMatches)}.");
                continue;
            }

            yield return new Diagnostic(
                DiagnosticLevel.Error,
                "KES2010",
                document.ProjectRelativePath,
                reference.Line,
                reference.Column,
                $"Undefined name '{reference.Name}'.");
        }
    }

    private static IEnumerable<IdentifierReference> GetIdentifierReferences(ScriptSyntax syntax)
    {
        foreach (var statement in syntax.Statements)
        {
            foreach (var reference in GetIdentifierReferences(statement))
            {
                yield return reference;
            }
        }
    }

    private static IEnumerable<IdentifierReference> GetIdentifierReferences(StatementSyntax statement)
    {
        switch (statement)
        {
            case VarStatementSyntax varStatement:
                foreach (var reference in FromTokens(varStatement.ValueTokens))
                {
                    yield return reference;
                }

                break;

            case CommandStatementSyntax commandStatement:
                foreach (var reference in FromTokens(commandStatement.Arguments))
                {
                    yield return reference;
                }

                break;

            case LessStatementSyntax lessStatement:
                foreach (var reference in FromLessStatement(lessStatement))
                {
                    yield return reference;
                }

                break;
        }
    }

    private static IEnumerable<TagReference> GetTagReferences(ScriptSyntax syntax)
    {
        foreach (var statement in syntax.Statements)
        {
            foreach (var reference in GetTagReferences(statement))
            {
                yield return reference;
            }
        }
    }

    private static IEnumerable<TagReference> GetTagReferences(StatementSyntax statement)
    {
        switch (statement)
        {
            case JumpStatementSyntax jumpStatement:
                yield return new TagReference(
                    jumpStatement.Tag,
                    jumpStatement.TagLocation.Line,
                    jumpStatement.TagLocation.Column);
                break;

            case SelectStatementSyntax selectStatement:
                foreach (var caseClause in selectStatement.Cases)
                {
                    yield return new TagReference(
                        caseClause.Tag,
                        caseClause.TagLocation.Line,
                        caseClause.TagLocation.Column);
                }

                break;
        }
    }

    private static IEnumerable<IdentifierReference> FromLessStatement(LessStatementSyntax statement)
    {
        foreach (var reference in FromTokens(statement.SharedArguments))
        {
            yield return reference;
        }

        foreach (var item in statement.Items)
        {
            switch (item)
            {
                case LessCommandItemSyntax commandItem:
                    foreach (var reference in FromTokens(commandItem.Arguments))
                    {
                        yield return reference;
                    }

                    break;

                case LessNestedStatementSyntax nestedStatement:
                    foreach (var reference in FromLessStatement(nestedStatement.Statement))
                    {
                        yield return reference;
                    }

                    break;
            }
        }
    }

    private static IEnumerable<IdentifierReference> FromTokens(IEnumerable<Token> tokens)
    {
        return tokens
            .Where(static token => token.Kind == TokenKind.Identifier)
            .Select(static token => new IdentifierReference(token.Lexeme, token.Line, token.Column));
    }

    private static string FormatModules(IEnumerable<SymbolDefinition> symbols)
    {
        return string.Join(", ", symbols
            .Select(static symbol => symbol.ModuleName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));
    }

    private static bool IsTagName(string name)
    {
        return name.StartsWith('#');
    }

    private sealed record IdentifierReference(string Name, int Line, int Column);

    private sealed record TagReference(string Name, int Line, int Column);
}
