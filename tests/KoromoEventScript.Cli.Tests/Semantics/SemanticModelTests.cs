using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class SemanticModelTests
{
    [Test]
    public void ScriptDocument_StoresProjectPathModuleNameAndSyntax()
    {
        var syntax = new ScriptSyntax([new ImportStatementSyntax("Common")]);

        var document = new ScriptDocument("events/main.ke", "Main", syntax);

        Assert.Multiple(() =>
        {
            Assert.That(document.ProjectRelativePath, Is.EqualTo("events/main.ke"));
            Assert.That(document.ModuleName, Is.EqualTo("Main"));
            Assert.That(document.Syntax, Is.SameAs(syntax));
        });
    }

    [Test]
    public void ImportGraph_StoresOrderedDocumentsAndDirectImportEdges()
    {
        var main = new ScriptDocument("events/main.ke", "Main", new ScriptSyntax([]));
        var common = new ScriptDocument("events/common.ke", "Common", new ScriptSyntax([]));

        var graph = new ImportGraph(
            [main, common],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Main"] = ["Common"],
                ["Common"] = [],
            });

        Assert.Multiple(() =>
        {
            Assert.That(graph.OrderedDocuments.Select(static document => document.ModuleName), Is.EqualTo(["Main", "Common"]));
            Assert.That(graph.DirectImports["Main"], Is.EqualTo(["Common"]));
            Assert.That(graph.DirectImports["Common"], Is.Empty);
        });
    }

    [Test]
    public void ImportGraph_ReturnsTransitiveImportsInStableFirstSeenOrder()
    {
        var main = new ScriptDocument("events/main.ke", "Main", new ScriptSyntax([]));
        var feature = new ScriptDocument("events/feature.ke", "Feature", new ScriptSyntax([]));
        var shared = new ScriptDocument("events/shared.ke", "Shared", new ScriptSyntax([]));
        var leaf = new ScriptDocument("events/leaf.ke", "Leaf", new ScriptSyntax([]));

        var graph = new ImportGraph(
            [main, feature, shared, leaf],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Main"] = ["Feature", "Shared"],
                ["Feature"] = ["Shared", "Leaf"],
                ["Shared"] = ["Leaf"],
                ["Leaf"] = [],
            });

        Assert.That(graph.GetReachableImports("Main"), Is.EqualTo(["Feature", "Shared", "Leaf"]));
    }

    [Test]
    public void ImportGraph_TreatsDuplicatePathsAsOneDependency()
    {
        var main = new ScriptDocument("events/main.ke", "Main", new ScriptSyntax([]));
        var left = new ScriptDocument("events/left.ke", "Left", new ScriptSyntax([]));
        var right = new ScriptDocument("events/right.ke", "Right", new ScriptSyntax([]));
        var shared = new ScriptDocument("events/shared.ke", "Shared", new ScriptSyntax([]));

        var graph = new ImportGraph(
            [main, left, right, shared],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Main"] = ["Left", "Right"],
                ["Left"] = ["Shared"],
                ["Right"] = ["Shared"],
                ["Shared"] = [],
            });

        Assert.That(graph.GetReachableImports("Main"), Is.EqualTo(["Left", "Shared", "Right"]));
    }

    [Test]
    public void ImportGraph_RetainsCyclePathForDiagnostics()
    {
        var main = new ScriptDocument("events/main.ke", "Main", new ScriptSyntax([]));
        var common = new ScriptDocument("events/common.ke", "Common", new ScriptSyntax([]));
        var shared = new ScriptDocument("events/shared.ke", "Shared", new ScriptSyntax([]));

        var graph = new ImportGraph(
            [main, common, shared],
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["Main"] = ["Common"],
                ["Common"] = ["Shared"],
                ["Shared"] = ["Main"],
            });

        Assert.Multiple(() =>
        {
            Assert.That(graph.Cycles, Has.Count.EqualTo(1));
            Assert.That(graph.Cycles[0].Modules, Is.EqualTo(["Main", "Common", "Shared", "Main"]));
        });
    }

    [Test]
    public void SymbolResult_StoresDocumentSymbolsAndDiagnostics()
    {
        var document = new ScriptDocument("events/common.ke", "Common", new ScriptSyntax([]));
        var symbol = new SymbolDefinition("shared", "Common", "events/common.ke", 3, 5);
        var diagnostic = Error("KES2001", "events/common.ke", 3, 5, "duplicate definition");

        var result = new DefinitionCollectionResult(document, [symbol], [diagnostic]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Document, Is.SameAs(document));
            Assert.That(result.Symbols, Is.EqualTo([symbol]));
            Assert.That(result.Diagnostics, Is.EqualTo([diagnostic]));
        });
    }

    [Test]
    public void DefinitionTable_StoresScopesAndScopedDefinitions()
    {
        var moduleScope = new DefinitionScope("Main:Module:0", ScopeKind.Module, null, "Main");
        var classScope = new DefinitionScope("Main:Class:1", ScopeKind.Class, moduleScope.Id, "Counter");
        var definition = new ScopedSymbolDefinition(
            "Counter",
            DefinitionKind.Class,
            "Main",
            "events/main.ke",
            3,
            7,
            moduleScope.Id);

        var table = new DefinitionTable(moduleScope.Id, [moduleScope, classScope], [definition]);

        Assert.Multiple(() =>
        {
            Assert.That(table.ModuleScopeId, Is.EqualTo(moduleScope.Id));
            Assert.That(table.Scopes.Select(static scope => scope.Kind), Is.EqualTo([ScopeKind.Module, ScopeKind.Class]));
            Assert.That(table.Scopes[1].ParentId, Is.EqualTo(moduleScope.Id));
            Assert.That(table.Definitions.Single(), Is.EqualTo(definition));
        });
    }

    [Test]
    public void SemanticAnalysisResult_PreservesDiagnosticsInStageOrder()
    {
        var importDiagnostic = Error("KES2002", "events/main.ke", 1, 1, "missing import");
        var nameDiagnostic = Error("KES2003", "events/main.ke", 4, 9, "undefined name");
        var importResult = ImportResolutionResult.Failure(CliExitCode.CompileError, [importDiagnostic]);
        var nameResult = NameResolutionResult.Failure(CliExitCode.CompileError, [nameDiagnostic]);

        var result = SemanticAnalysisResult.From(importResult, nameResult);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Is.EqualTo([importDiagnostic, nameDiagnostic]));
            Assert.That(result.ImportResolution, Is.SameAs(importResult));
            Assert.That(result.NameResolution, Is.SameAs(nameResult));
        });
    }

    [Test]
    public void SemanticAnalysisResult_UsesImportStageExitCodeBeforeNameResolutionExitCode()
    {
        var importDiagnostic = Error("KES9004", "events/common.ke", 1, 1, "could not read import");
        var nameDiagnostic = Error("KES2003", "events/main.ke", 4, 9, "undefined name");
        var importResult = ImportResolutionResult.Failure(CliExitCode.FileOrDirectoryError, [importDiagnostic]);
        var nameResult = NameResolutionResult.Failure(CliExitCode.CompileError, [nameDiagnostic]);

        var result = SemanticAnalysisResult.From(importResult, nameResult);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.FileOrDirectoryError));
            Assert.That(result.Diagnostics, Is.EqualTo([importDiagnostic, nameDiagnostic]));
        });
    }

    [Test]
    public void SemanticAnalysisResult_UsesNameResolutionExitCodeWhenImportsSucceeded()
    {
        var main = new ScriptDocument("events/main.ke", "Main", new ScriptSyntax([]));
        var graph = new ImportGraph([main], new Dictionary<string, IReadOnlyList<string>> { ["Main"] = [] });
        var nameDiagnostic = Error("KES2003", "events/main.ke", 4, 9, "undefined name");
        var importResult = ImportResolutionResult.Success(graph);
        var nameResult = NameResolutionResult.Failure(CliExitCode.CompileError, [nameDiagnostic]);

        var result = SemanticAnalysisResult.From(importResult, nameResult);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Is.EqualTo([nameDiagnostic]));
            Assert.That(result.ImportGraph, Is.SameAs(graph));
        });
    }

    private static Diagnostic Error(string code, string file, int line, int column, string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, code, file, line, column, message);
    }
}
