using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Lexing;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class NameResolverTests
{
    [Test]
    public void ResolveNames_AllowsReferencesToReachableImportedDefinitions()
    {
        var main = Document("events/main.ke", "Main", new CommandStatementSyntax("use", [Identifier("sharedValue", 4, 9)]));
        var common = Document("events/common.ke", "Common");
        var shared = Document("events/shared.ke", "Shared");
        var graph = Graph([main, common, shared], new Dictionary<string, IReadOnlyList<string>>
        {
            ["Main"] = ["Common"],
            ["Common"] = ["Shared"],
            ["Shared"] = [],
        });

        var result = new NameResolver().ResolveNames(
            graph,
            Symbols(
                Definition("sharedValue", "Shared", 2, 5)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.Success));
            Assert.That(result.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public void ResolveNames_ReportsUnimportedDefinitionsAsUndefinedReferences()
    {
        var main = Document("events/main.ke", "Main", new CommandStatementSyntax("use", [Identifier("hiddenOnly", 7, 13)]));
        var common = Document("events/common.ke", "Common");
        var hidden = Document("events/hidden.ke", "Hidden");
        var graph = Graph([main, common, hidden], new Dictionary<string, IReadOnlyList<string>>
        {
            ["Main"] = ["Common"],
            ["Common"] = [],
            ["Hidden"] = [],
        });

        var result = new NameResolver().ResolveNames(
            graph,
            Symbols(
                Definition("visible", "Common", 2, 5),
                Definition("hiddenOnly", "Hidden", 3, 5)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2010"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(7));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(13));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("hiddenOnly"));
        });
    }

    [Test]
    public void ResolveNames_ReportsLocalImportDefinitionCollisions()
    {
        var main = Document("events/main.ke", "Main");
        var common = Document("events/common.ke", "Common");
        var graph = Graph([main, common], new Dictionary<string, IReadOnlyList<string>>
        {
            ["Main"] = ["Common"],
            ["Common"] = [],
        });

        var result = new NameResolver().ResolveNames(
            graph,
            Symbols(
                Definition("sharedName", "Main", 4, 5),
                Definition("sharedName", "Common", 2, 5)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2011"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(4));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(5));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("sharedName"));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("Common"));
        });
    }

    [Test]
    public void ResolveNames_ReportsAmbiguousReferencesFromMultipleImportedDefinitions()
    {
        var main = Document("events/main.ke", "Main", new CommandStatementSyntax("use", [Identifier("sharedName", 8, 17)]));
        var left = Document("events/left.ke", "Left");
        var right = Document("events/right.ke", "Right");
        var graph = Graph([main, left, right], new Dictionary<string, IReadOnlyList<string>>
        {
            ["Main"] = ["Left", "Right"],
            ["Left"] = [],
            ["Right"] = [],
        });

        var result = new NameResolver().ResolveNames(
            graph,
            Symbols(
                Definition("sharedName", "Left", 2, 5),
                Definition("sharedName", "Right", 3, 5)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo("KES2012"));
            Assert.That(result.Diagnostics[0].File, Is.EqualTo("events/main.ke"));
            Assert.That(result.Diagnostics[0].Line, Is.EqualTo(8));
            Assert.That(result.Diagnostics[0].Column, Is.EqualTo(17));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("sharedName"));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("Left"));
            Assert.That(result.Diagnostics[0].Message, Does.Contain("Right"));
        });
    }

    [Test]
    public void ResolveNames_UsesLocalDefinitionBeforeImportedDefinitionForReferences()
    {
        var main = Document("events/main.ke", "Main", new CommandStatementSyntax("use", [Identifier("localOnly", 6, 11)]));
        var common = Document("events/common.ke", "Common");
        var graph = Graph([main, common], new Dictionary<string, IReadOnlyList<string>>
        {
            ["Main"] = ["Common"],
            ["Common"] = [],
        });

        var result = new NameResolver().ResolveNames(
            graph,
            Symbols(
                Definition("localOnly", "Main", 2, 5),
                Definition("importedOnly", "Common", 2, 5)));

        Assert.That(result.Diagnostics, Is.Empty);
    }

    private static ScriptDocument Document(string file, string moduleName, params StatementSyntax[] statements)
    {
        return new ScriptDocument(file, moduleName, new ScriptSyntax(statements));
    }

    private static ImportGraph Graph(
        IReadOnlyList<ScriptDocument> documents,
        IReadOnlyDictionary<string, IReadOnlyList<string>> directImports)
    {
        return new ImportGraph(documents, directImports);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<SymbolDefinition>> Symbols(params SymbolDefinition[] symbols)
    {
        return symbols
            .GroupBy(static symbol => symbol.ModuleName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<SymbolDefinition>)group.ToArray(),
                StringComparer.Ordinal);
    }

    private static SymbolDefinition Definition(string name, string moduleName, int line, int column)
    {
        return new SymbolDefinition(name, moduleName, $"events/{moduleName.ToLowerInvariant()}.ke", line, column);
    }

    private static Token Identifier(string lexeme, int line, int column)
    {
        return new Token(TokenKind.Identifier, lexeme, line, column);
    }
}
