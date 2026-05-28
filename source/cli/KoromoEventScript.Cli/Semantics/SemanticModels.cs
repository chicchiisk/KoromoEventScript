using System.Collections.ObjectModel;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Semantics;

public sealed record ScriptDocument
{
    public ScriptDocument(string projectRelativePath, string moduleName, ScriptSyntax syntax)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(syntax);

        ProjectRelativePath = projectRelativePath.Replace('\\', '/');
        ModuleName = moduleName;
        Syntax = syntax;
    }

    public string ProjectRelativePath { get; }

    public string ModuleName { get; }

    public ScriptSyntax Syntax { get; }
}

public sealed record ImportGraph
{
    public ImportGraph(
        IReadOnlyList<ScriptDocument> orderedDocuments,
        IReadOnlyDictionary<string, IReadOnlyList<string>> directImports)
    {
        ArgumentNullException.ThrowIfNull(orderedDocuments);
        ArgumentNullException.ThrowIfNull(directImports);

        OrderedDocuments = orderedDocuments.ToArray();
        DirectImports = CopyDirectImports(directImports);
        Cycles = DetectCycles(OrderedDocuments, DirectImports);
    }

    public IReadOnlyList<ScriptDocument> OrderedDocuments { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DirectImports { get; }

    public IReadOnlyList<ImportCyclePath> Cycles { get; }

    public IReadOnlyList<string> GetReachableImports(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var reachable = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        CollectReachableImports(moduleName, visited, reachable);
        return reachable;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CopyDirectImports(
        IReadOnlyDictionary<string, IReadOnlyList<string>> directImports)
    {
        var copy = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (moduleName, imports) in directImports)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
            ArgumentNullException.ThrowIfNull(imports);
            copy[moduleName] = imports
                .Where(static import => !string.IsNullOrWhiteSpace(import))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
    }

    private void CollectReachableImports(
        string moduleName,
        HashSet<string> visited,
        List<string> reachable)
    {
        if (!DirectImports.TryGetValue(moduleName, out var imports))
        {
            return;
        }

        foreach (var importedModule in imports)
        {
            if (!visited.Add(importedModule))
            {
                continue;
            }

            reachable.Add(importedModule);
            CollectReachableImports(importedModule, visited, reachable);
        }
    }

    private static IReadOnlyList<ImportCyclePath> DetectCycles(
        IReadOnlyList<ScriptDocument> orderedDocuments,
        IReadOnlyDictionary<string, IReadOnlyList<string>> directImports)
    {
        var cycles = new List<ImportCyclePath>();
        var seenCycles = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var activeIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var activePath = new List<string>();

        foreach (var document in orderedDocuments)
        {
            Visit(document.ModuleName);
        }

        return cycles;

        void Visit(string moduleName)
        {
            if (activeIndexes.TryGetValue(moduleName, out var cycleStart))
            {
                var modules = activePath
                    .Skip(cycleStart)
                    .Concat([moduleName])
                    .ToArray();
                var key = string.Join('\u001f', modules);
                if (seenCycles.Add(key))
                {
                    cycles.Add(new ImportCyclePath(modules));
                }

                return;
            }

            if (!visited.Add(moduleName))
            {
                return;
            }

            activeIndexes[moduleName] = activePath.Count;
            activePath.Add(moduleName);

            if (directImports.TryGetValue(moduleName, out var imports))
            {
                foreach (var importedModule in imports)
                {
                    Visit(importedModule);
                }
            }

            activeIndexes.Remove(moduleName);
            activePath.RemoveAt(activePath.Count - 1);
        }
    }
}

public sealed record ImportCyclePath
{
    public ImportCyclePath(IReadOnlyList<string> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (modules.Count < 2)
        {
            throw new ArgumentException("Cycle paths must contain at least one edge.", nameof(modules));
        }

        Modules = modules.ToArray();
    }

    public IReadOnlyList<string> Modules { get; }
}

public sealed record ImportResolutionResult
{
    private ImportResolutionResult(
        CliExitCode exitCode,
        IReadOnlyList<Diagnostic> diagnostics,
        ImportGraph? importGraph)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        ExitCode = exitCode;
        Diagnostics = diagnostics.ToArray();
        ImportGraph = importGraph;
    }

    public CliExitCode ExitCode { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public ImportGraph? ImportGraph { get; }

    public bool Succeeded => ExitCode == CliExitCode.Success;

    public static ImportResolutionResult Success(ImportGraph importGraph)
    {
        ArgumentNullException.ThrowIfNull(importGraph);
        return new ImportResolutionResult(CliExitCode.Success, [], importGraph);
    }

    public static ImportResolutionResult Failure(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (exitCode == CliExitCode.Success)
        {
            throw new ArgumentException("Failure results must carry a non-success exit code.", nameof(exitCode));
        }

        return new ImportResolutionResult(exitCode, diagnostics, null);
    }
}

public sealed record SymbolDefinition(
    string Name,
    string ModuleName,
    string File,
    int Line,
    int Column);

public sealed record DefinitionCollectionResult
{
    public DefinitionCollectionResult(
        ScriptDocument document,
        IReadOnlyList<SymbolDefinition> symbols,
        IReadOnlyList<Diagnostic> diagnostics,
        DefinitionTable? definitionTable = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Document = document;
        Symbols = symbols.ToArray();
        Diagnostics = diagnostics.ToArray();
        DefinitionTable = definitionTable ?? new DefinitionTable(
            $"{document.ModuleName}:module",
            [new DefinitionScope($"{document.ModuleName}:module", ScopeKind.Module, null, document.ModuleName)],
            symbols
                .Select(static symbol => new ScopedSymbolDefinition(
                    symbol.Name,
                    DefinitionKind.Variable,
                    symbol.ModuleName,
                    symbol.File,
                    symbol.Line,
                    symbol.Column,
                    $"{symbol.ModuleName}:module"))
                .ToArray());
    }

    public ScriptDocument Document { get; }

    public IReadOnlyList<SymbolDefinition> Symbols { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public DefinitionTable DefinitionTable { get; }

    public bool Succeeded => Diagnostics.Count == 0;
}

public sealed record NameResolutionResult
{
    private NameResolutionResult(
        CliExitCode exitCode,
        IReadOnlyList<Diagnostic> diagnostics,
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> symbolsByModule)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(symbolsByModule);

        ExitCode = exitCode;
        Diagnostics = diagnostics.ToArray();
        SymbolsByModule = CopySymbols(symbolsByModule);
    }

    public CliExitCode ExitCode { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> SymbolsByModule { get; }

    public bool Succeeded => ExitCode == CliExitCode.Success;

    public static NameResolutionResult Success(
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>>? symbolsByModule = null)
    {
        return new NameResolutionResult(CliExitCode.Success, [], symbolsByModule ?? new Dictionary<string, IReadOnlyList<SymbolDefinition>>());
    }

    public static NameResolutionResult Failure(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (exitCode == CliExitCode.Success)
        {
            throw new ArgumentException("Failure results must carry a non-success exit code.", nameof(exitCode));
        }

        return new NameResolutionResult(exitCode, diagnostics, new Dictionary<string, IReadOnlyList<SymbolDefinition>>());
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> CopySymbols(
        IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> symbolsByModule)
    {
        var copy = new Dictionary<string, IReadOnlyList<SymbolDefinition>>(StringComparer.Ordinal);
        foreach (var (moduleName, symbols) in symbolsByModule)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
            ArgumentNullException.ThrowIfNull(symbols);
            copy[moduleName] = symbols.ToArray();
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>>(copy);
    }
}

public sealed record WarningAnalysisResult
{
    public WarningAnalysisResult(IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        Diagnostics = diagnostics.ToArray();
    }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
}

public sealed record SemanticAnalysisResult
{
    private SemanticAnalysisResult(
        CliExitCode exitCode,
        IReadOnlyList<Diagnostic> diagnostics,
        ImportResolutionResult importResolution,
        NameResolutionResult nameResolution,
        TypeCheckingResult typeChecking,
        WarningAnalysisResult warningAnalysis,
        IReadOnlyList<DefinitionCollectionResult> definitionCollections)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(importResolution);
        ArgumentNullException.ThrowIfNull(nameResolution);
        ArgumentNullException.ThrowIfNull(typeChecking);
        ArgumentNullException.ThrowIfNull(warningAnalysis);
        ArgumentNullException.ThrowIfNull(definitionCollections);

        ExitCode = exitCode;
        Diagnostics = diagnostics.ToArray();
        ImportResolution = importResolution;
        NameResolution = nameResolution;
        TypeChecking = typeChecking;
        WarningAnalysis = warningAnalysis;
        DefinitionCollections = definitionCollections.ToArray();
    }

    public CliExitCode ExitCode { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public ImportResolutionResult ImportResolution { get; }

    public NameResolutionResult NameResolution { get; }

    public TypeCheckingResult TypeChecking { get; }

    public WarningAnalysisResult WarningAnalysis { get; }

    public IReadOnlyList<DefinitionCollectionResult> DefinitionCollections { get; }

    public ImportGraph? ImportGraph => ImportResolution.ImportGraph;

    public bool Succeeded => ExitCode == CliExitCode.Success;

    public static SemanticAnalysisResult From(
        ImportResolutionResult importResolution,
        NameResolutionResult nameResolution,
        IReadOnlyList<DefinitionCollectionResult>? definitionCollections = null)
    {
        ArgumentNullException.ThrowIfNull(importResolution);
        ArgumentNullException.ThrowIfNull(nameResolution);

        var exitCode = importResolution.ExitCode == CliExitCode.Success
            ? nameResolution.ExitCode == CliExitCode.Success
                ? TypeCheckingResult.Success().ExitCode
                : nameResolution.ExitCode
            : importResolution.ExitCode;

        var diagnostics = importResolution.Diagnostics
            .Concat(nameResolution.Diagnostics)
            .ToArray();

        return new SemanticAnalysisResult(
            exitCode,
            diagnostics,
            importResolution,
            nameResolution,
            TypeCheckingResult.Success(),
            new WarningAnalysisResult([]),
            definitionCollections ?? []);
    }

    public static SemanticAnalysisResult From(
        ImportResolutionResult importResolution,
        NameResolutionResult nameResolution,
        TypeCheckingResult typeChecking,
        IReadOnlyList<DefinitionCollectionResult>? definitionCollections = null)
    {
        return From(importResolution, nameResolution, typeChecking, new WarningAnalysisResult([]), definitionCollections);
    }

    public static SemanticAnalysisResult From(
        ImportResolutionResult importResolution,
        NameResolutionResult nameResolution,
        TypeCheckingResult typeChecking,
        WarningAnalysisResult? warningAnalysis = null,
        IReadOnlyList<DefinitionCollectionResult>? definitionCollections = null)
    {
        ArgumentNullException.ThrowIfNull(importResolution);
        ArgumentNullException.ThrowIfNull(nameResolution);
        ArgumentNullException.ThrowIfNull(typeChecking);

        var exitCode = importResolution.ExitCode == CliExitCode.Success
            ? nameResolution.ExitCode == CliExitCode.Success
                ? typeChecking.ExitCode
                : nameResolution.ExitCode
            : importResolution.ExitCode;

        warningAnalysis ??= new WarningAnalysisResult([]);

        var diagnostics = importResolution.Diagnostics
            .Concat(nameResolution.Diagnostics)
            .Concat(typeChecking.Diagnostics)
            .Concat(warningAnalysis.Diagnostics)
            .ToArray();

        return new SemanticAnalysisResult(exitCode, diagnostics, importResolution, nameResolution, typeChecking, warningAnalysis, definitionCollections ?? []);
    }
}
