using KoromoEventScript.Cli.Commands;
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
            .ToArray();
        var symbolsByModule = definitionResults.ToDictionary(
            static result => result.Document.ModuleName,
            static result => result.Symbols,
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
}
