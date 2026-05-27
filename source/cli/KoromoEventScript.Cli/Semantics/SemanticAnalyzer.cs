using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Semantics;

public sealed class SemanticAnalyzer
{
    private readonly ModuleFileIndex moduleFileIndex;
    private readonly ImportResolver importResolver;
    private readonly DefinitionCollector definitionCollector;
    private readonly NameResolver nameResolver;

    public SemanticAnalyzer()
        : this(new ModuleFileIndex(), new ImportResolver(), new DefinitionCollector(), new NameResolver())
    {
    }

    public SemanticAnalyzer(
        ModuleFileIndex moduleFileIndex,
        ImportResolver importResolver,
        DefinitionCollector definitionCollector,
        NameResolver nameResolver)
    {
        ArgumentNullException.ThrowIfNull(moduleFileIndex);
        ArgumentNullException.ThrowIfNull(importResolver);
        ArgumentNullException.ThrowIfNull(definitionCollector);
        ArgumentNullException.ThrowIfNull(nameResolver);

        this.moduleFileIndex = moduleFileIndex;
        this.importResolver = importResolver;
        this.definitionCollector = definitionCollector;
        this.nameResolver = nameResolver;
    }

    public SemanticAnalysisResult Analyze(
        ProjectConfig config,
        IReadOnlyList<ScriptDocument> entryDocuments)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(entryDocuments);

        var moduleIndex = moduleFileIndex.Build(config).Index;
        var importResult = importResolver.ResolveImports(moduleIndex, entryDocuments);
        if (!importResult.Succeeded)
        {
            return SemanticAnalysisResult.From(importResult, NameResolutionResult.Success());
        }

        var graph = importResult.ImportGraph!;
        var definitionResults = graph.OrderedDocuments
            .Select(definitionCollector.Collect)
            .ToArray();
        var definitionDiagnostics = definitionResults
            .SelectMany(static result => result.Diagnostics)
            .Concat(DetectDuplicateModuleDefinitionsAcrossDocuments(definitionResults))
            .ToArray();
        var symbolsByModule = definitionResults
            .GroupBy(static result => result.Document.ModuleName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<SymbolDefinition>)group.SelectMany(static result => result.Symbols).ToArray(),
                StringComparer.Ordinal);

        if (definitionDiagnostics.Length > 0)
        {
            return SemanticAnalysisResult.From(
                importResult,
                NameResolutionResult.Failure(CliExitCode.CompileError, definitionDiagnostics),
                definitionResults);
        }

        var nameResult = nameResolver.ResolveNames(graph, symbolsByModule);
        return SemanticAnalysisResult.From(importResult, nameResult, definitionResults);
    }

    private static IEnumerable<Diagnostic> DetectDuplicateModuleDefinitionsAcrossDocuments(
        IReadOnlyList<DefinitionCollectionResult> definitionResults)
    {
        foreach (var moduleGroup in definitionResults.GroupBy(static result => result.Document.ModuleName, StringComparer.Ordinal))
        {
            var firstDefinitionsByName = new Dictionary<string, ScopedSymbolDefinition>(StringComparer.Ordinal);
            foreach (var result in moduleGroup)
            {
                foreach (var definition in result.DefinitionTable.Definitions.Where(IsModuleScopeMajorDefinition))
                {
                    if (!firstDefinitionsByName.TryGetValue(definition.Name, out var original))
                    {
                        firstDefinitionsByName[definition.Name] = definition;
                        continue;
                    }

                    if (string.Equals(original.File, definition.File, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return DuplicateDefinitionDiagnostic(result.Document.ModuleName, original, definition);
                }
            }
        }
    }

    private static bool IsModuleScopeMajorDefinition(ScopedSymbolDefinition definition)
    {
        return definition.Kind is DefinitionKind.Actor
            or DefinitionKind.Function
            or DefinitionKind.Class
            or DefinitionKind.Enum
            or DefinitionKind.Variable;
    }

    private static Diagnostic DuplicateDefinitionDiagnostic(
        string moduleName,
        ScopedSymbolDefinition original,
        ScopedSymbolDefinition duplicate)
    {
        return new Diagnostic(
            DiagnosticLevel.Error,
            "KES2009",
            duplicate.File,
            duplicate.Line,
            duplicate.Column,
            $"Duplicate definition '{duplicate.Name}' in module '{moduleName}'.",
            [
                new DiagnosticRelatedLocation(
                    original.File,
                    original.Line,
                    original.Column,
                    "Original definition is here.")
            ]);
    }
}
