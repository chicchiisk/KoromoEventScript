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
    }

    public IReadOnlyList<ScriptDocument> OrderedDocuments { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DirectImports { get; }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CopyDirectImports(
        IReadOnlyDictionary<string, IReadOnlyList<string>> directImports)
    {
        var copy = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (moduleName, imports) in directImports)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
            ArgumentNullException.ThrowIfNull(imports);
            copy[moduleName] = imports.ToArray();
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
    }
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
        IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Document = document;
        Symbols = symbols.ToArray();
        Diagnostics = diagnostics.ToArray();
    }

    public ScriptDocument Document { get; }

    public IReadOnlyList<SymbolDefinition> Symbols { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }
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

public sealed record SemanticAnalysisResult
{
    private SemanticAnalysisResult(
        CliExitCode exitCode,
        IReadOnlyList<Diagnostic> diagnostics,
        ImportResolutionResult importResolution,
        NameResolutionResult nameResolution)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(importResolution);
        ArgumentNullException.ThrowIfNull(nameResolution);

        ExitCode = exitCode;
        Diagnostics = diagnostics.ToArray();
        ImportResolution = importResolution;
        NameResolution = nameResolution;
    }

    public CliExitCode ExitCode { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public ImportResolutionResult ImportResolution { get; }

    public NameResolutionResult NameResolution { get; }

    public ImportGraph? ImportGraph => ImportResolution.ImportGraph;

    public bool Succeeded => ExitCode == CliExitCode.Success;

    public static SemanticAnalysisResult From(
        ImportResolutionResult importResolution,
        NameResolutionResult nameResolution)
    {
        ArgumentNullException.ThrowIfNull(importResolution);
        ArgumentNullException.ThrowIfNull(nameResolution);

        var exitCode = importResolution.ExitCode == CliExitCode.Success
            ? nameResolution.ExitCode
            : importResolution.ExitCode;

        var diagnostics = importResolution.Diagnostics
            .Concat(nameResolution.Diagnostics)
            .ToArray();

        return new SemanticAnalysisResult(exitCode, diagnostics, importResolution, nameResolution);
    }
}
