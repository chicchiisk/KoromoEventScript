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
        var firstDefinitionsByName = new Dictionary<string, SymbolDefinition>(StringComparer.Ordinal);

        foreach (var statement in document.Syntax.Statements)
        {
            foreach (var definition in GetTopLevelDefinitions(statement))
            {
                var symbol = CreateSymbol(document, definition);
                symbols.Add(symbol);

                if (firstDefinitionsByName.TryAdd(symbol.Name, symbol))
                {
                    continue;
                }

                diagnostics.Add(DuplicateDefinitionDiagnostic(document, symbol));
            }
        }

        return new DefinitionCollectionResult(document, symbols, diagnostics);
    }

    private static IEnumerable<CollectedDefinition> GetTopLevelDefinitions(StatementSyntax statement)
    {
        switch (statement)
        {
            case VarStatementSyntax varStatement:
                yield return new CollectedDefinition(varStatement.Name, varStatement.NameLocation);
                break;

            case LabelStatementSyntax labelStatement:
                yield return new CollectedDefinition(labelStatement.Tag, labelStatement.TagLocation);
                break;

            case SayStatementSyntax { Tag: { Length: > 0 } tag, TagLocation: { } location }:
                yield return new CollectedDefinition(tag, location);
                break;

            case NarStatementSyntax { Tag: { Length: > 0 } tag, TagLocation: { } location }:
                yield return new CollectedDefinition(tag, location);
                break;
        }
    }

    private static SymbolDefinition CreateSymbol(ScriptDocument document, CollectedDefinition definition)
    {
        return new SymbolDefinition(
            definition.Name,
            document.ModuleName,
            document.ProjectRelativePath,
            definition.Location.Line,
            definition.Location.Column);
    }

    private static Diagnostic DuplicateDefinitionDiagnostic(ScriptDocument document, SymbolDefinition duplicate)
    {
        return new Diagnostic(
            DiagnosticLevel.Error,
            "KES2009",
            duplicate.File,
            duplicate.Line,
            duplicate.Column,
            $"Duplicate top-level definition '{duplicate.Name}' in module '{document.ModuleName}'.");
    }

    private sealed record CollectedDefinition(string Name, SourceLocation Location);
}
